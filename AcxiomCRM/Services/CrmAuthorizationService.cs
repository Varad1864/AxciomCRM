using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface ICrmAuthorizationService
    {
        Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal);
        Task<string> GetUserRoleAsync(ApplicationUser user);
        bool IsAdmin(ClaimsPrincipal principal);
        bool IsManager(ClaimsPrincipal principal);
        bool IsSalesExecutive(ClaimsPrincipal principal);
        IQueryable<Customer> FilterCustomers(IQueryable<Customer> query, ClaimsPrincipal principal, string? userId);
        IQueryable<Lead> FilterLeads(IQueryable<Lead> query, ClaimsPrincipal principal, string? userId);
        IQueryable<Opportunity> FilterOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal principal, string? userId);
        IQueryable<FollowUp> FilterFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal principal, string? userId);
        IQueryable<Activity> FilterActivities(IQueryable<Activity> query, ClaimsPrincipal principal, string? userId);
    }

    public class CrmAuthorizationService : ICrmAuthorizationService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CrmAuthorizationService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal)
        {
            return await _userManager.GetUserAsync(principal);
        }

        public async Task<string> GetUserRoleAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.FirstOrDefault() ?? "SalesExecutive";
        }

        public bool IsAdmin(ClaimsPrincipal principal) => principal.IsInRole("Admin");
        public bool IsManager(ClaimsPrincipal principal) => principal.IsInRole("Manager");
        public bool IsSalesExecutive(ClaimsPrincipal principal) => principal.IsInRole("SalesExecutive");

        public IQueryable<Customer> FilterCustomers(IQueryable<Customer> query, ClaimsPrincipal principal, string? userId)
        {
            if (IsAdmin(principal) || IsManager(principal))
                return query;

            // SalesExecutive: only assigned customers
            return query.Where(c => c.AssignedTo == userId);
        }

        public IQueryable<Lead> FilterLeads(IQueryable<Lead> query, ClaimsPrincipal principal, string? userId)
        {
            if (IsAdmin(principal) || IsManager(principal))
                return query;

            return query.Where(l => l.AssignedTo == userId);
        }

        public IQueryable<Opportunity> FilterOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal principal, string? userId)
        {
            if (IsAdmin(principal) || IsManager(principal))
                return query;

            return query.Where(o => o.AssignedTo == userId);
        }

        public IQueryable<FollowUp> FilterFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal principal, string? userId)
        {
            if (IsAdmin(principal) || IsManager(principal))
                return query;

            return query.Where(f => f.AssignedTo == userId);
        }

        public IQueryable<Activity> FilterActivities(IQueryable<Activity> query, ClaimsPrincipal principal, string? userId)
        {
            if (IsAdmin(principal) || IsManager(principal))
                return query;

            return query.Where(a => a.AssignedTo == userId);
        }
    }
}
