using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;

        public ReportsApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAuthorizationService authService)
        {
            _context = context;
            _userManager = userManager;
            _authService = authService;
        }

        // GET: api/reports/pipeline
        [HttpGet("pipeline")]
        public async Task<ActionResult<PipelineReportDto>> GetPipelineReport()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var opps = await _authService.FilterOpportunities(
                _context.Opportunities.Include(o => o.AssignedUser),
                User, user.Id)
                .ToListAsync();

            var openOpps = opps.Where(o => o.Stage != "Won" && o.Stage != "Lost").ToList();

            var stageSummaries = opps
                .GroupBy(o => o.Stage)
                .Select(g => new StageSummaryDto
                {
                    Stage = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = g.Sum(x => x.Amount * x.Probability / 100m)
                })
                .ToList();

            var ownerSummaries = opps
                .GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
                .Select(g => new OwnerSummaryDto
                {
                    OwnerName = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            var report = new PipelineReportDto
            {
                TotalPipelineValue = openOpps.Sum(o => o.Amount),
                TotalWeightedValue = openOpps.Sum(o => o.Amount * o.Probability / 100m),
                TotalOpportunitiesCount = opps.Count,
                StageSummaries = stageSummaries,
                OwnerSummaries = ownerSummaries
            };

            return Ok(report);
        }
    }
}
