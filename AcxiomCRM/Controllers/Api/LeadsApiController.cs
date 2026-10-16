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
    [Route("api/leads")]
    [Authorize]
    public class LeadsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public LeadsApiController(
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

        // GET: api/leads
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeadResponseDto>>> GetLeads([FromQuery] string? search, [FromQuery] string? status)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _authService.FilterLeads(_context.Leads.Include(l => l.AssignedUser).AsQueryable(), User, user.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(l => l.LeadName.Contains(search) || l.Email.Contains(search) || l.Phone.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var list = await query
                .OrderByDescending(l => l.CreatedDate)
                .Select(l => new LeadResponseDto
                {
                    LeadId = l.LeadId,
                    LeadCode = l.LeadCode,
                    LeadName = l.LeadName,
                    Email = l.Email,
                    Phone = l.Phone,
                    CompanyName = l.CompanyName,
                    Source = l.Source,
                    Status = l.Status,
                    Priority = l.Priority,
                    ExpectedValue = l.ExpectedValue,
                    CreatedDate = l.CreatedDate,
                    AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: api/leads
        [HttpPost]
        public async Task<ActionResult<LeadResponseDto>> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            string assignedTo = _authService.IsSalesExecutive(User) ? user.Id : (dto.AssignedTo ?? user.Id);

            var lead = new Lead
            {
                LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source ?? "Website",
                Status = dto.Status ?? "New",
                Priority = dto.Priority ?? "Medium",
                ExpectedValue = dto.ExpectedValue,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedTo
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("API Create", "Lead", lead.LeadId.ToString(), null,
                $"Name: {lead.LeadName}, ExpectedValue: {lead.ExpectedValue}", "Lead created via API", "Success", user.Id, user.Email);

            var responseDto = new LeadResponseDto
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                CreatedDate = lead.CreatedDate
            };

            return CreatedAtAction(nameof(GetLeads), new { id = lead.LeadId }, responseDto);
        }
    }
}
