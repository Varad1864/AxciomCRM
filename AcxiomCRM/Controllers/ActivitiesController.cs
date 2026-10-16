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
    public class ActivitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public ActivitiesController(
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

        // GET: Activities
        public async Task<IActionResult> Index(string? activityType, string? status, string? assignedTo, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _authService.FilterActivities(
                _context.Activities
                    .Include(a => a.Customer)
                    .Include(a => a.Lead)
                    .Include(a => a.AssignedUser)
                    .AsQueryable(),
                User, user.Id);

            if (!string.IsNullOrWhiteSpace(activityType))
            {
                query = query.Where(a => a.ActivityType == activityType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(a => a.AssignedTo == assignedTo);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var activities = await query
                .OrderByDescending(a => a.ActivityDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentType = activityType;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            await PopulateDropDownLists();
            return View(activities);
        }

        // GET: Activities/Create
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            await PopulateDropDownLists(customerId, leadId);
            var act = new Activity
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityDate = DateTime.UtcNow,
                ActivityType = "Call",
                Status = "Completed"
            };
            return View(act);
        }

        // POST: Activities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Activity activity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (_authService.IsSalesExecutive(User))
            {
                activity.AssignedTo = user.Id;
            }

            if (ModelState.IsValid)
            {
                _context.Add(activity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Activity", activity.ActivityId.ToString(), null,
                    $"Type: {activity.ActivityType}, Subject: {activity.Subject}",
                    "Activity logged.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = "Activity logged successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownLists(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        private async Task PopulateDropDownLists(int? customerId = null, int? leadId = null, object? selectedUser = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);

            var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);

            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}
