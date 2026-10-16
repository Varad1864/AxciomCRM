using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAuthorizationService authService)
        {
            _context = context;
            _userManager = userManager;
            _authService = authService;
        }

        public async Task<IActionResult> Index(string dateFilter = "AllTime")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            string role = await _authService.GetUserRoleAsync(user);
            string userId = user.Id;

            // Base queries filtered by role
            var customersQuery = _authService.FilterCustomers(_context.Customers.AsQueryable(), User, userId);
            var leadsQuery = _authService.FilterLeads(_context.Leads.AsQueryable(), User, userId);
            var oppsQuery = _authService.FilterOpportunities(_context.Opportunities.Include(o => o.Customer).AsQueryable(), User, userId);
            var followupsQuery = _authService.FilterFollowUps(_context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsQueryable(), User, userId);
            var activitiesQuery = _authService.FilterActivities(_context.Activities.Include(a => a.Customer).Include(a => a.Lead).AsQueryable(), User, userId);

            // Apply Date Filter if specified
            DateTime? startDate = null;
            var now = DateTime.UtcNow;

            switch (dateFilter.ToLower())
            {
                case "today":
                    startDate = now.Date;
                    break;
                case "thisweek":
                    startDate = now.Date.AddDays(-(int)now.DayOfWeek);
                    break;
                case "thismonth":
                    startDate = new DateTime(now.Year, now.Month, 1);
                    break;
                case "alltime":
                default:
                    dateFilter = "AllTime";
                    break;
            }

            if (startDate.HasValue)
            {
                customersQuery = customersQuery.Where(c => c.CreatedDate >= startDate.Value);
                leadsQuery = leadsQuery.Where(l => l.CreatedDate >= startDate.Value);
                oppsQuery = oppsQuery.Where(o => o.CreatedDate >= startDate.Value);
                followupsQuery = followupsQuery.Where(f => f.FollowUpDate >= startDate.Value);
                activitiesQuery = activitiesQuery.Where(a => a.ActivityDate >= startDate.Value);
            }

            // Materialize or compute KPIs
            var totalCustomers = await customersQuery.CountAsync();
            var totalLeads = await leadsQuery.CountAsync();
            var openLeads = await leadsQuery.CountAsync(l => l.Status != "Converted" && l.Status != "Lost" && l.Status != "Unqualified");

            // Materialize opportunities in scope into memory for safe decimal calculation in SQLite
            var oppsList = await oppsQuery
                .Select(o => new { o.OpportunityId, o.Stage, o.Status, o.Amount, o.Probability, o.ExpectedCloseDate, o.CreatedDate, o.OpportunityName })
                .ToListAsync();

            var totalOpps = oppsList.Count;
            var openOpps = oppsList.Count(o => o.Status == "Open" || (o.Stage != "Won" && o.Stage != "Lost"));
            var wonOpps = oppsList.Count(o => o.Stage == "Won" || o.Status == "Won");
            var lostOpps = oppsList.Count(o => o.Stage == "Lost" || o.Status == "Lost");

            var totalPipeline = oppsList
                .Where(o => o.Stage != "Won" && o.Stage != "Lost")
                .Sum(o => o.Amount);

            var weightedPipeline = oppsList
                .Where(o => o.Stage != "Won" && o.Stage != "Lost")
                .Sum(o => (o.Amount * o.Probability / 100m));

            var pendingFollowUps = await followupsQuery.CountAsync(f => f.Status == "Planned");
            var overdueFollowUps = await followupsQuery.CountAsync(f => f.Status == "Planned" && f.FollowUpDate < now);

            // Chart 1: Lead Status
            var leadStatuses = new[] { "New", "Contacted", "Qualified", "Converted", "Lost" };
            var leadStatusGrouped = await leadsQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var leadStatusCounts = leadStatuses
                .Select(s => leadStatusGrouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0)
                .ToList();

            // Chart 2: Opportunity Pipeline Stage Breakdown
            var pipelineStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var oppStageGrouped = oppsList
                .GroupBy(o => o.Stage)
                .Select(g => new { Stage = g.Key, Amount = g.Sum(x => x.Amount), Count = g.Count() })
                .ToList();

            var pipelineStageAmounts = pipelineStages
                .Select(s => oppStageGrouped.FirstOrDefault(g => g.Stage == s)?.Amount ?? 0m)
                .ToList();

            var pipelineStageCounts = pipelineStages
                .Select(s => oppStageGrouped.FirstOrDefault(g => g.Stage == s)?.Count ?? 0)
                .ToList();

            // Chart 3: Monthly Sales / Won Totals (last 6 months)
            var monthlySalesLabels = new List<string>();
            var monthlySalesValues = new List<decimal>();

            var allWonOpps = await _context.Opportunities
                .Where(o => o.Stage == "Won" || o.Status == "Won")
                .Select(o => new { o.Amount, o.ExpectedCloseDate })
                .ToListAsync();

            for (int i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var monthWonTotal = allWonOpps
                    .Where(o => o.ExpectedCloseDate >= monthStart && o.ExpectedCloseDate < monthEnd)
                    .Sum(o => o.Amount);

                monthlySalesLabels.Add(monthDate.ToString("MMM yyyy"));
                monthlySalesValues.Add(monthWonTotal);
            }

            // Quick Lists for Dashboard
            var upcomingFollowUpsList = await followupsQuery
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            var recentOppsList = await oppsQuery
                .OrderByDescending(o => o.CreatedDate)
                .Take(5)
                .ToListAsync();

            var recentLeadsList = await leadsQuery
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToListAsync();

            var recentActivitiesList = await activitiesQuery
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync();

            var vm = new DashboardViewModel
            {
                UserRole = role,
                UserName = user.FullName ?? user.UserName ?? "User",
                DateFilter = dateFilter,
                TotalCustomers = totalCustomers,
                TotalLeads = totalLeads,
                OpenLeads = openLeads,
                TotalOpportunities = totalOpps,
                OpenOpportunities = openOpps,
                WonOpportunities = wonOpps,
                LostOpportunities = lostOpps,
                TotalPipelineValue = totalPipeline,
                WeightedPipelineValue = weightedPipeline,
                PendingFollowUps = pendingFollowUps,
                OverdueFollowUps = overdueFollowUps,
                ActiveUsersCount = await _userManager.Users.CountAsync(u => u.IsActive),

                LeadStatusLabels = leadStatuses.ToList(),
                LeadStatusData = leadStatusCounts,

                PipelineStageLabels = pipelineStages.ToList(),
                PipelineStageAmounts = pipelineStageAmounts,
                PipelineStageCounts = pipelineStageCounts,

                MonthlySalesLabels = monthlySalesLabels,
                MonthlySalesValues = monthlySalesValues,

                UpcomingFollowUps = upcomingFollowUpsList,
                RecentOpportunities = recentOppsList,
                RecentLeads = recentLeadsList,
                RecentActivities = recentActivitiesList
            };

            return View(vm);
        }
    }
}
