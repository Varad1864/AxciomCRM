using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AuditLogs
        public async Task<IActionResult> Index(string? actionFilter, string? moduleFilter, string? searchUser, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            if (!string.IsNullOrWhiteSpace(moduleFilter))
            {
                query = query.Where(a => a.EntityName == moduleFilter);
            }

            if (!string.IsNullOrWhiteSpace(searchUser))
            {
                searchUser = searchUser.Trim();
                query = query.Where(a => (a.UserName != null && a.UserName.Contains(searchUser)) ||
                                         (a.UserId != null && a.UserId.Contains(searchUser)));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            int pageSize = 15;
            int totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentAction = actionFilter;
            ViewBag.CurrentModule = moduleFilter;
            ViewBag.SearchUser = searchUser;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(logs);
        }
    }
}
