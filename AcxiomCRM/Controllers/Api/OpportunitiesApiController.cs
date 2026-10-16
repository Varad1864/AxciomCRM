using System;
using System.Collections.Generic;
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
    [Route("api/opportunities")]
    [Authorize]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public OpportunitiesApiController(
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

        // GET: api/opportunities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OpportunityResponseDto>>> GetOpportunities([FromQuery] string? stage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _authService.FilterOpportunities(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.Lead)
                    .Include(o => o.AssignedUser)
                    .AsQueryable(),
                User, user.Id);

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage == stage);
            }

            var list = await query
                .OrderByDescending(o => o.CreatedDate)
                .Select(o => new OpportunityResponseDto
                {
                    OpportunityId = o.OpportunityId,
                    OpportunityName = o.OpportunityName,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                    LeadId = o.LeadId,
                    LeadName = o.Lead != null ? o.Lead.LeadName : null,
                    Amount = o.Amount,
                    Stage = o.Stage,
                    Probability = o.Probability,
                    WeightedPipeline = (o.Amount * o.Probability) / 100m,
                    ExpectedCloseDate = o.ExpectedCloseDate,
                    Status = o.Status,
                    CreatedDate = o.CreatedDate,
                    AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null,
                    Notes = o.Notes
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: api/opportunities
        [HttpPost]
        public async Task<ActionResult<OpportunityResponseDto>> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Mandatory Business Validation Rules
            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
            }

            if (dto.Probability < 0 || dto.Probability > 100)
            {
                return BadRequest(new { message = "Probability must be between 0 and 100." });
            }

            if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { message = "Expected Close Date cannot be in the past." });
            }

            string assignedTo = _authService.IsSalesExecutive(User) ? user.Id : (dto.AssignedTo ?? user.Id);

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage ?? "Qualification",
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = dto.Stage == "Won" ? "Won" : (dto.Stage == "Lost" ? "Lost" : "Open"),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedTo,
                Notes = dto.Notes
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("API Create", "Opportunity", opp.OpportunityId.ToString(), null,
                $"Name: {opp.OpportunityName}, Amount: {opp.Amount}", "Opportunity created via API", "Success", user.Id, user.Email);

            var responseDto = new OpportunityResponseDto
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                WeightedPipeline = (opp.Amount * opp.Probability) / 100m,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                CreatedDate = opp.CreatedDate,
                Notes = opp.Notes
            };

            return CreatedAtAction(nameof(GetOpportunities), new { id = opp.OpportunityId }, responseDto);
        }
    }
}
