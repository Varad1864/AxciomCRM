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
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public LeadsController(
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

        // GET: Leads
        public async Task<IActionResult> Index(string? search, string? status, string? source, string? assignedTo, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _authService.FilterLeads(_context.Leads.Include(l => l.AssignedUser).AsQueryable(), User, user.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(l => l.LeadName.Contains(search) ||
                                         l.Email.Contains(search) ||
                                         l.Phone.Contains(search) ||
                                         (l.CompanyName != null && l.CompanyName.Contains(search)) ||
                                         l.LeadCode.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                query = query.Where(l => l.Source == source);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(l => l.AssignedTo == assignedTo);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var leads = await query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSource = source;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            await PopulateUsersDropDownList();
            return View(leads);
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _authService.FilterLeads(_context.Leads.Include(l => l.AssignedUser), User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return Forbid();

            ViewBag.FollowUps = await _context.FollowUps
                .Where(f => f.LeadId == id)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            ViewBag.Activities = await _context.Activities
                .Where(a => a.LeadId == id)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            return View(lead);
        }

        // GET: Leads/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUsersDropDownList();
            var lead = new Lead
            {
                LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
                Status = "New",
                Priority = "Medium",
                Source = "Website"
            };
            return View(lead);
        }

        // POST: Leads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (lead.ExpectedValue < 0)
            {
                ModelState.AddModelError("ExpectedValue", "Expected Value must be greater than or equal to 0.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                lead.AssignedTo = user.Id;
            }

            if (ModelState.IsValid)
            {
                lead.CreatedDate = DateTime.UtcNow;
                if (string.IsNullOrWhiteSpace(lead.LeadCode))
                {
                    lead.LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}";
                }

                _context.Add(lead);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(), null,
                    $"Name: {lead.LeadName}, Company: {lead.CompanyName}, Status: {lead.Status}",
                    "New Lead captured.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _authService.FilterLeads(_context.Leads, User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return Forbid();

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.LeadId) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingLead = await _authService.FilterLeads(_context.Leads.AsNoTracking(), User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (existingLead == null) return Forbid();

            if (_authService.IsSalesExecutive(User))
            {
                lead.AssignedTo = existingLead.AssignedTo;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    lead.LeadCode = existingLead.LeadCode;
                    lead.CreatedDate = existingLead.CreatedDate;
                    lead.ConvertedCustomerId = existingLead.ConvertedCustomerId;
                    lead.ConvertedOpportunityId = existingLead.ConvertedOpportunityId;

                    _context.Update(lead);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Update", "Lead", lead.LeadId.ToString(),
                        $"Status: {existingLead.Status}, ExpectedValue: {existingLead.ExpectedValue}",
                        $"Status: {lead.Status}, ExpectedValue: {lead.ExpectedValue}",
                        "Lead record updated.", "Success", user.Id, user.Email);

                    TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' was updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeadExists(lead.LeadId)) return NotFound();
                    else throw;
                }
            }

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        // GET: Leads/Convert/5
        public async Task<IActionResult> Convert(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _authService.FilterLeads(_context.Leads, User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return Forbid();

            if (lead.Status == "Converted")
            {
                TempData["ErrorMessage"] = "This lead is already converted.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            var vm = new LeadConvertViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                CreateCustomer = true,
                CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
                CustomerName = string.IsNullOrWhiteSpace(lead.CompanyName) ? lead.LeadName : lead.CompanyName,
                CreateOpportunity = true,
                OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - Sales Engagement",
                OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
                OpportunityStage = "Qualification",
                OpportunityProbability = 30,
                OpportunityCloseDate = DateTime.UtcNow.AddDays(30),
                AssignedTo = lead.AssignedTo
            };

            await PopulateUsersDropDownList(vm.AssignedTo);
            return View(vm);
        }

        // POST: Leads/Convert/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(LeadConvertViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _authService.FilterLeads(_context.Leads, User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == model.LeadId);

            if (lead == null) return Forbid();

            if (lead.Status == "Converted")
            {
                TempData["ErrorMessage"] = "This lead has already been converted.";
                return RedirectToAction(nameof(Index));
            }

            // Validations
            if (model.CreateCustomer)
            {
                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    ModelState.AddModelError("CustomerName", "Customer name is required.");
                }
                if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
                {
                    ModelState.AddModelError("Email", "A customer with this email address already exists.");
                }
            }

            if (model.CreateOpportunity)
            {
                if (model.OpportunityAmount <= 0)
                {
                    ModelState.AddModelError("OpportunityAmount", "Opportunity amount must be greater than 0.");
                }
                if (model.OpportunityProbability < 0 || model.OpportunityProbability > 100)
                {
                    ModelState.AddModelError("OpportunityProbability", "Probability must be between 0 and 100.");
                }
                if (model.OpportunityCloseDate.Date < DateTime.UtcNow.Date)
                {
                    ModelState.AddModelError("OpportunityCloseDate", "Expected Close Date cannot be in the past.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateUsersDropDownList(model.AssignedTo);
                return View(model);
            }

            Customer? newCustomer = null;
            if (model.CreateCustomer)
            {
                newCustomer = new Customer
                {
                    CustomerCode = string.IsNullOrWhiteSpace(model.CustomerCode)
                        ? $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}"
                        : model.CustomerCode,
                    CustomerName = model.CustomerName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = user.FullName ?? user.UserName,
                    AssignedTo = model.AssignedTo ?? lead.AssignedTo
                };
                _context.Customers.Add(newCustomer);
                await _context.SaveChangesAsync();
            }

            Opportunity? newOpp = null;
            if (model.CreateOpportunity)
            {
                newOpp = new Opportunity
                {
                    OpportunityName = model.OpportunityName ?? $"{lead.LeadName} Opportunity",
                    CustomerId = newCustomer?.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount,
                    Stage = model.OpportunityStage,
                    Probability = model.OpportunityProbability,
                    ExpectedCloseDate = model.OpportunityCloseDate,
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = model.AssignedTo ?? lead.AssignedTo,
                    Notes = $"Converted from Lead {lead.LeadCode}."
                };
                _context.Opportunities.Add(newOpp);
                await _context.SaveChangesAsync();
            }

            // Update Lead status
            lead.Status = "Converted";
            lead.ConvertedCustomerId = newCustomer?.CustomerId;
            lead.ConvertedOpportunityId = newOpp?.OpportunityId;
            _context.Update(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Lead Conversion", "Lead", lead.LeadId.ToString(),
                $"Status: Qualified",
                $"Status: Converted, CustomerId: {newCustomer?.CustomerId}, OpportunityId: {newOpp?.OpportunityId}",
                $"Lead {lead.LeadCode} converted successfully.", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' was successfully converted!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Leads/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _authService.FilterLeads(_context.Leads, User, user.Id)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return Forbid();

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Lead", id.ToString(),
                $"Name: {lead.LeadName}, Code: {lead.LeadCode}", null,
                "Lead deleted.", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' was removed.";
            return RedirectToAction(nameof(Index));
        }

        private bool LeadExists(int id)
        {
            return _context.Leads.Any(e => e.LeadId == id);
        }

        private async Task PopulateUsersDropDownList(object? selectedUser = null)
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}
