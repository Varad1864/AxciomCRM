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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public OpportunitiesController(
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

        // GET: Opportunities
        public async Task<IActionResult> Index(string? search, string? stage, string? status, string? assignedTo, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _authService.FilterOpportunities(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.Lead)
                    .Include(o => o.AssignedUser)
                    .AsQueryable(),
                User, user.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(o => o.OpportunityName.Contains(search) ||
                                         (o.Customer != null && o.Customer.CustomerName.Contains(search)) ||
                                         (o.Lead != null && o.Lead.LeadName.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage == stage);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(o => o.AssignedTo == assignedTo);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var opps = await query
                .OrderByDescending(o => o.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStage = stage;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            await PopulateDropDownLists();
            return View(opps);
        }

        // GET: Opportunities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var opp = await _authService.FilterOpportunities(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.Lead)
                    .Include(o => o.AssignedUser),
                User, user.Id)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return Forbid();

            ViewBag.FollowUps = await _context.FollowUps
                .Where(f => (opp.CustomerId != null && f.CustomerId == opp.CustomerId) || (opp.LeadId != null && f.LeadId == opp.LeadId))
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            return View(opp);
        }

        // GET: Opportunities/Create
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            await PopulateDropDownLists(customerId: customerId, leadId: leadId);
            var opp = new Opportunity
            {
                CustomerId = customerId,
                LeadId = leadId,
                Stage = "Qualification",
                Probability = 20,
                Amount = 10000m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Status = "Open"
            };
            return View(opp);
        }

        // POST: Opportunities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Mandatory Business Validation Rules
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != "Won" && opportunity.Stage != "Lost")
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                opportunity.AssignedTo = user.Id;
            }

            // Sync Status
            if (opportunity.Stage == "Won") opportunity.Status = "Won";
            else if (opportunity.Stage == "Lost") opportunity.Status = "Lost";
            else opportunity.Status = "Open";

            if (ModelState.IsValid)
            {
                opportunity.CreatedDate = DateTime.UtcNow;
                _context.Add(opportunity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Opportunity", opportunity.OpportunityId.ToString(), null,
                    $"Name: {opportunity.OpportunityName}, Amount: {opportunity.Amount:C}, Stage: {opportunity.Stage}",
                    "Opportunity created.", "Success", user.Id, user.Email);

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownLists(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
            return View(opportunity);
        }

        // GET: Opportunities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var opportunity = await _authService.FilterOpportunities(_context.Opportunities, User, user.Id)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opportunity == null) return Forbid();

            await PopulateDropDownLists(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
            return View(opportunity);
        }

        // POST: Opportunities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.OpportunityId) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingOpp = await _authService.FilterOpportunities(_context.Opportunities.AsNoTracking(), User, user.Id)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (existingOpp == null) return Forbid();

            // Mandatory Business Validation Rules
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != "Won" && opportunity.Stage != "Lost")
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (_authService.IsSalesExecutive(User))
            {
                opportunity.AssignedTo = existingOpp.AssignedTo;
            }

            // Sync Status
            if (opportunity.Stage == "Won") opportunity.Status = "Won";
            else if (opportunity.Stage == "Lost") opportunity.Status = "Lost";
            else opportunity.Status = "Open";

            if (ModelState.IsValid)
            {
                try
                {
                    opportunity.CreatedDate = existingOpp.CreatedDate;
                    _context.Update(opportunity);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Update", "Opportunity", opportunity.OpportunityId.ToString(),
                        $"Stage: {existingOpp.Stage}, Amount: {existingOpp.Amount}",
                        $"Stage: {opportunity.Stage}, Amount: {opportunity.Amount}",
                        "Opportunity updated.", "Success", user.Id, user.Email);

                    TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OpportunityExists(opportunity.OpportunityId)) return NotFound();
                    else throw;
                }
            }

            await PopulateDropDownLists(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
            return View(opportunity);
        }

        // POST: Opportunities/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var opp = await _authService.FilterOpportunities(_context.Opportunities, User, user.Id)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return Forbid();

            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Opportunity", id.ToString(),
                $"Name: {opp.OpportunityName}, Amount: {opp.Amount}", null,
                "Opportunity deleted.", "Success", user.Id, user.Email);

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' removed.";
            return RedirectToAction(nameof(Index));
        }

        private bool OpportunityExists(int id)
        {
            return _context.Opportunities.Any(e => e.OpportunityId == id);
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
