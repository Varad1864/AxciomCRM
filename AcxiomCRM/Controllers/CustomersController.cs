using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public CustomersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAuthorizationService authService,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _authService = authService;
            _auditService = auditService;
        }

        // GET: Customers
        public async Task<IActionResult> Index(string? search, string? status, string? assignedTo, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _authService.FilterCustomers(_context.Customers.Include(c => c.AssignedUser).AsQueryable(), User, user.Id);

            // Filtering
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => c.CustomerName.Contains(search) ||
                                         c.Email.Contains(search) ||
                                         c.Phone.Contains(search) ||
                                         (c.CompanyName != null && c.CompanyName.Contains(search)) ||
                                         c.CustomerCode.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(c => c.AssignedTo == assignedTo);
            }

            // Pagination
            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var customers = await query
                .OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            await PopulateUsersDropDownList();
            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var customer = await _authService.FilterCustomers(_context.Customers.Include(c => c.AssignedUser), User, user.Id)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                return Forbid();
            }

            // Related opportunities, followups, activities
            ViewBag.Opportunities = await _context.Opportunities
                .Where(o => o.CustomerId == id)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            ViewBag.FollowUps = await _context.FollowUps
                .Where(f => f.CustomerId == id)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            ViewBag.Activities = await _context.Activities
                .Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            return View(customer);
        }

        // GET: Customers/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUsersDropDownList();
            var customer = new Customer
            {
                CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
                Status = "Active"
            };
            return View(customer);
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Business & Server-side Validations
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                // SalesExecutive can only assign to self
                customer.AssignedTo = user.Id;
            }

            if (ModelState.IsValid)
            {
                customer.CreatedDate = DateTime.UtcNow;
                customer.CreatedBy = user.FullName ?? user.UserName;

                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    customer.CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}";
                }

                _context.Add(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null,
                    $"Name: {customer.CustomerName}, Email: {customer.Email}, Phone: {customer.Phone}",
                    "Customer record created successfully.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownList(customer.AssignedTo);
            return View(customer);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var customer = await _authService.FilterCustomers(_context.Customers, User, user.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return Forbid();

            await PopulateUsersDropDownList(customer.AssignedTo);
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.CustomerId) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingCustomer = await _authService.FilterCustomers(_context.Customers.AsNoTracking(), User, user.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (existingCustomer == null) return Forbid();

            // Uniqueness validation on edit (exclude current record)
            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == customer.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Another customer with this email address already exists.");
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == customer.Phone))
            {
                ModelState.AddModelError("Phone", "Another customer with this phone number already exists.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                customer.AssignedTo = existingCustomer.AssignedTo;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    customer.CustomerCode = existingCustomer.CustomerCode;
                    customer.CreatedDate = existingCustomer.CreatedDate;
                    customer.CreatedBy = existingCustomer.CreatedBy;

                    _context.Update(customer);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(),
                        $"Status: {existingCustomer.Status}, Name: {existingCustomer.CustomerName}",
                        $"Status: {customer.Status}, Name: {customer.CustomerName}",
                        "Customer record updated successfully.", "Success", user.Id, user.Email);

                    TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerId)) return NotFound();
                    else throw;
                }
            }

            await PopulateUsersDropDownList(customer.AssignedTo);
            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var customer = await _authService.FilterCustomers(_context.Customers, User, user.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return Forbid();

            // Check if customer has opportunities
            bool hasOpps = await _context.Opportunities.AnyAsync(o => o.CustomerId == id);
            if (hasOpps)
            {
                // Soft deactivate if linked data exists
                customer.Status = "Inactive";
                _context.Update(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Deactivate", "Customer", id.ToString(), null, "Status: Inactive",
                    "Customer deactivated because active opportunities exist.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' has associated opportunities and was marked Inactive.";
            }
            else
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Delete", "Customer", id.ToString(),
                    $"Name: {customer.CustomerName}, Code: {customer.CustomerCode}", null,
                    "Customer record deleted.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was removed.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }

        private async Task PopulateUsersDropDownList(object? selectedUser = null)
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}
