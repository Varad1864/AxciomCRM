using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        // GET: Users
        public async Task<IActionResult> Index(string? search, string? roleFilter)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));
            }

            var users = await query.OrderByDescending(u => u.CreatedDate).ToListAsync();
            var viewList = new System.Collections.Generic.List<UserItemViewModel>();

            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                string primaryRole = userRoles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrWhiteSpace(roleFilter) && primaryRole != roleFilter)
                {
                    continue;
                }

                viewList.Add(new UserItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email!,
                    Role = primaryRole,
                    IsActive = user.IsActive,
                    IsLockedOut = await _userManager.IsLockedOutAsync(user),
                    LockoutEnd = user.LockoutEnd,
                    FailedLoginCount = user.AccessFailedCount,
                    CreatedDate = user.CreatedDate
                });
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentRole = roleFilter;
            return View(viewList);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View(new RegisterViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "A user with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(model.Role));
                }
                await _userManager.AddToRoleAsync(user, model.Role);

                var currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync("Create User", "User", user.Id, null,
                    $"Role: {model.Role}, Email: {user.Email}",
                    $"Admin created user {user.FullName} with role {model.Role}.", "Success", currentUser?.Id, currentUser?.Email);

                TempData["SuccessMessage"] = $"User '{user.FullName}' was created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "SalesExecutive",
                IsActive = user.IsActive,
                UnlockAccount = false
            };

            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            string oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "None";

            user.FullName = model.FullName;
            user.IsActive = model.IsActive;

            if (model.UnlockAccount)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                await _auditService.LogAsync("Unlock Account", "Security", user.Id, null, null,
                    $"Admin unlocked account for {user.Email}", "Success", currentUser?.Id, currentUser?.Email);
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                // Update Role if changed
                if (oldRole != model.Role)
                {
                    if (!string.IsNullOrEmpty(oldRole))
                    {
                        await _userManager.RemoveFromRoleAsync(user, oldRole);
                    }
                    if (!await _roleManager.RoleExistsAsync(model.Role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(model.Role));
                    }
                    await _userManager.AddToRoleAsync(user, model.Role);

                    await _auditService.LogAsync("Role Change", "User", user.Id, oldRole, model.Role,
                        $"User role changed from {oldRole} to {model.Role}.", "Success", currentUser?.Id, currentUser?.Email);
                }

                TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Users/ResetPassword/5
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new AdminResetPasswordViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email!
            };

            return View(model);
        }

        // POST: Users/ResetPassword/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                await _auditService.LogAsync("Security", "User", user.Id, null, null,
                    $"Admin reset password for user {user.Email}.", "Success", currentUser?.Id, currentUser?.Email);

                TempData["SuccessMessage"] = $"Password for '{user.Email}' was reset successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
