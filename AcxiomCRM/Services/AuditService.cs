using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task LogAsync(
            string action,
            string entityName,
            string? recordId = null,
            string? oldValue = null,
            string? newValue = null,
            string? details = null,
            string? result = "Success",
            string? specificUserId = null,
            string? specificUserName = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                string? userId = specificUserId;
                string? userName = specificUserName;

                if (string.IsNullOrEmpty(userId) && httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                    userName = httpContext.User.Identity.Name;
                }

                string? ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

                var audit = new AuditLog
                {
                    UserId = userId,
                    UserName = userName ?? "System/Anonymous",
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Details = details,
                    Result = result ?? "Success",
                    IpAddress = ipAddress,
                    CreatedDate = DateTime.UtcNow
                };

                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Never allow audit logging failure to crash primary business flow
            }
        }
    }
}
