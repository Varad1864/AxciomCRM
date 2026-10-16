using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAuthorizationService authService)
        {
            _context = context;
            _userManager = userManager;
            _authService = authService;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/Pipeline
        public async Task<IActionResult> Pipeline()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var opps = await _authService.FilterOpportunities(_context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser), User, user.Id)
                .ToListAsync();

            var stageSummaries = opps
                .GroupBy(o => o.Stage)
                .Select(g => new
                {
                    Stage = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = g.Sum(x => x.Amount * x.Probability / 100m)
                })
                .ToList();

            var ownerSummaries = opps
                .GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
                .Select(g => new
                {
                    Owner = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            ViewBag.StageSummaries = stageSummaries;
            ViewBag.OwnerSummaries = ownerSummaries;
            ViewBag.TotalPipeline = opps.Where(o => o.Stage != "Won" && o.Stage != "Lost").Sum(o => o.Amount);
            ViewBag.TotalWeighted = opps.Where(o => o.Stage != "Won" && o.Stage != "Lost").Sum(o => o.Amount * o.Probability / 100m);

            return View(opps);
        }

        // GET: Reports/Conversion
        public async Task<IActionResult> Conversion()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var leads = await _authService.FilterLeads(_context.Leads, User, user.Id).ToListAsync();
            var opps = await _authService.FilterOpportunities(_context.Opportunities, User, user.Id).ToListAsync();

            int totalLeads = leads.Count;
            int convertedLeads = leads.Count(l => l.Status == "Converted");
            int qualifiedLeads = leads.Count(l => l.Status == "Qualified" || l.Status == "Converted");
            int lostLeads = leads.Count(l => l.Status == "Lost");

            int totalOpps = opps.Count;
            int wonOpps = opps.Count(o => o.Stage == "Won" || o.Status == "Won");
            int lostOpps = opps.Count(o => o.Stage == "Lost" || o.Status == "Lost");
            decimal wonValue = opps.Where(o => o.Stage == "Won" || o.Status == "Won").Sum(o => o.Amount);

            ViewBag.TotalLeads = totalLeads;
            ViewBag.ConvertedLeads = convertedLeads;
            ViewBag.ConversionRate = totalLeads > 0 ? (double)convertedLeads / totalLeads * 100 : 0;
            ViewBag.QualifiedLeads = qualifiedLeads;
            ViewBag.LostLeads = lostLeads;

            ViewBag.TotalOpps = totalOpps;
            ViewBag.WonOpps = wonOpps;
            ViewBag.LostOpps = lostOpps;
            ViewBag.OppWinRate = totalOpps > 0 ? (double)wonOpps / totalOpps * 100 : 0;
            ViewBag.WonValue = wonValue;

            return View();
        }

        // GET: Reports/FollowUps
        public async Task<IActionResult> FollowUps()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var followUps = await _authService.FilterFollowUps(
                _context.FollowUps
                    .Include(f => f.Customer)
                    .Include(f => f.Lead)
                    .Include(f => f.AssignedUser),
                User, user.Id)
                .OrderBy(f => f.FollowUpDate)
                .ToListAsync();

            var now = DateTime.UtcNow;
            ViewBag.PlannedCount = followUps.Count(f => f.Status == "Planned");
            ViewBag.CompletedCount = followUps.Count(f => f.Status == "Completed");
            ViewBag.OverdueCount = followUps.Count(f => f.Status == "Planned" && f.FollowUpDate < now);
            ViewBag.MissedCount = followUps.Count(f => f.Status == "Missed");

            return View(followUps);
        }

        // GET: Reports/UserActivity (Admin / Manager)
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UserActivity()
        {
            var users = await _userManager.Users.ToListAsync();
            var userStats = new System.Collections.Generic.List<dynamic>();

            foreach (var u in users)
            {
                var customersCount = await _context.Customers.CountAsync(c => c.AssignedTo == u.Id);
                var leadsCount = await _context.Leads.CountAsync(l => l.AssignedTo == u.Id);
                var oppsCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == u.Id);
                var activitiesCount = await _context.Activities.CountAsync(a => a.AssignedTo == u.Id);
                var followUpsCount = await _context.FollowUps.CountAsync(f => f.AssignedTo == u.Id);

                userStats.Add(new
                {
                    User = u,
                    Customers = customersCount,
                    Leads = leadsCount,
                    Opportunities = oppsCount,
                    Activities = activitiesCount,
                    FollowUps = followUpsCount
                });
            }

            ViewBag.UserStats = userStats;
            return View();
        }

        // GET: Reports/ExportCustomersCsv
        public async Task<IActionResult> ExportCustomersCsv()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var customers = await _authService.FilterCustomers(_context.Customers.Include(c => c.AssignedUser), User, user.Id)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("CustomerId,CustomerCode,CustomerName,Email,Phone,CompanyName,City,State,Status,AssignedTo,CreatedDate");

            foreach (var c in customers)
            {
                sb.AppendLine($"{c.CustomerId},\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.City}\",\"{c.State}\",\"{c.Status}\",\"{c.AssignedUser?.FullName}\",\"{c.CreatedDate:yyyy-MM-dd}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"Customers_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // GET: Reports/ExportOpportunitiesCsv
        public async Task<IActionResult> ExportOpportunitiesCsv()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var opps = await _authService.FilterOpportunities(_context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser), User, user.Id)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("OpportunityId,Name,Customer,Amount,Stage,Probability,WeightedPipeline,ExpectedCloseDate,Status,AssignedTo");

            foreach (var o in opps)
            {
                sb.AppendLine($"{o.OpportunityId},\"{o.OpportunityName}\",\"{o.Customer?.CustomerName}\",{o.Amount},\"{o.Stage}\",{o.Probability}%,{o.WeightedPipeline},\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.Status}\",\"{o.AssignedUser?.FullName}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"Opportunities_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}
