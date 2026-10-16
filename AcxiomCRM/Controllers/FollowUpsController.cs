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
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public FollowUpsController(
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

        // GET: FollowUps
        public async Task<IActionResult> Index(string? status, string? type, string? assignedTo, string? filterRange, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _authService.FilterFollowUps(
                _context.FollowUps
                    .Include(f => f.Customer)
                    .Include(f => f.Lead)
                    .Include(f => f.AssignedUser)
                    .AsQueryable(),
                User, user.Id);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(f => f.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(f => f.FollowUpType == type);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(f => f.AssignedTo == assignedTo);
            }

            var now = DateTime.UtcNow;
            if (filterRange == "upcoming")
            {
                query = query.Where(f => f.Status == "Planned" && f.FollowUpDate >= now);
            }
            else if (filterRange == "overdue")
            {
                query = query.Where(f => f.Status == "Planned" && f.FollowUpDate < now);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var followUps = await query
                .OrderBy(f => f.FollowUpDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.FilterRange = filterRange;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            await PopulateDropDownLists();
            return View(followUps);
        }

        // GET: FollowUps/Create
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            await PopulateDropDownLists(customerId, leadId);
            var followUp = new FollowUp
            {
                CustomerId = customerId,
                LeadId = leadId,
                RelatedType = customerId.HasValue ? "Customer" : (leadId.HasValue ? "Lead" : "Customer"),
                FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10), // default tomorrow 10am
                FollowUpType = "Call",
                Status = "Planned"
            };
            return View(followUp);
        }

        // POST: FollowUps/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Mandatory Business Validation: Follow-up date cannot be earlier than today for a new/planned follow-up
            if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                followUp.AssignedTo = user.Id;
            }

            if (ModelState.IsValid)
            {
                followUp.CreatedDate = DateTime.UtcNow;
                _context.Add(followUp);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "FollowUp", followUp.FollowUpId.ToString(), null,
                    $"Subject: {followUp.Subject}, Date: {followUp.FollowUpDate:d}, Type: {followUp.FollowUpType}",
                    "Follow-up scheduled.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = "Follow-up scheduled successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownLists(followUp.CustomerId, followUp.LeadId, followUp.AssignedTo);
            return View(followUp);
        }

        // POST: FollowUps/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? completionNotes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var followUp = await _authService.FilterFollowUps(_context.FollowUps, User, user.Id)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);

            if (followUp == null) return Forbid();

            followUp.Status = "Completed";
            if (!string.IsNullOrWhiteSpace(completionNotes))
            {
                followUp.Remarks = (followUp.Remarks != null ? followUp.Remarks + " | " : "") + $"Completed: {completionNotes}";
            }

            _context.Update(followUp);

            // Also create an Activity log entry automatically
            var activity = new Activity
            {
                ActivityType = followUp.FollowUpType,
                Subject = $"Completed Follow-up: {followUp.Subject}",
                Description = completionNotes ?? followUp.Remarks ?? "Follow-up marked completed.",
                ActivityDate = DateTime.UtcNow,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                AssignedTo = followUp.AssignedTo,
                Status = "Completed"
            };
            _context.Activities.Add(activity);

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Complete", "FollowUp", id.ToString(), "Status: Planned", "Status: Completed",
                $"Follow-up '{followUp.Subject}' completed.", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = "Follow-up marked as Completed.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Reschedule/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? rescheduleReason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var followUp = await _authService.FilterFollowUps(_context.FollowUps, User, user.Id)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);

            if (followUp == null) return Forbid();

            if (newDate.Date < DateTime.UtcNow.Date)
            {
                TempData["ErrorMessage"] = "Follow-up date cannot be earlier than today.";
                return RedirectToAction(nameof(Index));
            }

            var oldDate = followUp.FollowUpDate;
            followUp.FollowUpDate = newDate;
            followUp.Status = "Planned";
            followUp.Remarks = (followUp.Remarks != null ? followUp.Remarks + " | " : "") + $"Rescheduled from {oldDate:g}: {rescheduleReason}";

            _context.Update(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Reschedule", "FollowUp", id.ToString(),
                $"Date: {oldDate:g}", $"Date: {newDate:g}",
                $"Follow-up rescheduled. Reason: {rescheduleReason}", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = $"Follow-up rescheduled to {newDate:g}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var followUp = await _authService.FilterFollowUps(_context.FollowUps, User, user.Id)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);

            if (followUp == null) return Forbid();

            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "FollowUp", id.ToString(),
                $"Subject: {followUp.Subject}", null, "Follow-up removed.", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = "Follow-up deleted.";
            return RedirectToAction(nameof(Index));
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
