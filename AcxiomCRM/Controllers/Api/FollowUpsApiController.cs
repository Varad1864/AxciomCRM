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
    [Route("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public FollowUpsApiController(
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

        // GET: api/followups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FollowUpResponseDto>>> GetFollowUps([FromQuery] string? status)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

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

            var list = await query
                .OrderBy(f => f.FollowUpDate)
                .Select(f => new FollowUpResponseDto
                {
                    FollowUpId = f.FollowUpId,
                    RelatedType = f.RelatedType,
                    CustomerId = f.CustomerId,
                    CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                    LeadId = f.LeadId,
                    LeadName = f.Lead != null ? f.Lead.LeadName : null,
                    FollowUpDate = f.FollowUpDate,
                    FollowUpType = f.FollowUpType,
                    Subject = f.Subject,
                    Status = f.Status,
                    Remarks = f.Remarks,
                    AssignedUserName = f.AssignedUser != null ? f.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: api/followups
        [HttpPost]
        public async Task<ActionResult<FollowUpResponseDto>> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Mandatory Business Validation: Follow-up date cannot be earlier than today
            if (dto.Status == "Planned" && dto.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
            }

            string assignedTo = _authService.IsSalesExecutive(User) ? user.Id : (dto.AssignedTo ?? user.Id);

            var followUp = new FollowUp
            {
                RelatedType = dto.RelatedType,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Subject = dto.Subject,
                Status = dto.Status ?? "Planned",
                Remarks = dto.Remarks,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedTo
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("API Create", "FollowUp", followUp.FollowUpId.ToString(), null,
                $"Subject: {followUp.Subject}, Date: {followUp.FollowUpDate:d}", "Follow-up created via API", "Success", user.Id, user.Email);

            var responseDto = new FollowUpResponseDto
            {
                FollowUpId = followUp.FollowUpId,
                RelatedType = followUp.RelatedType,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpType = followUp.FollowUpType,
                Subject = followUp.Subject,
                Status = followUp.Status,
                Remarks = followUp.Remarks
            };

            return CreatedAtAction(nameof(GetFollowUps), new { id = followUp.FollowUpId }, responseDto);
        }
    }
}
