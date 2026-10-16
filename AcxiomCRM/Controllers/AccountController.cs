using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                await _auditService.LogAsync("Login Failed", "Auth", null, null, null, $"User with email {model.Email} not found.", "Failed", null, model.Email);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync("Login Blocked", "Auth", user.Id, null, null, "Attempted login to deactivated account.", "Failed", user.Id, user.Email);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an Administrator.");
                return View(model);
            }

            // SignIn with lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync("Login", "Auth", user.Id, null, null, "User successfully logged in.", "Success", user.Id, user.Email);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync("Account Locked", "Security", user.Id, null, null, "Account locked out due to excessive failed attempts.", "Failed", user.Id, user.Email);
                ModelState.AddModelError(string.Empty, "Account locked due to multiple failed login attempts. Please try again later or contact your administrator.");
                return View(model);
            }

            await _auditService.LogAsync("Login Failed", "Auth", user.Id, null, null, "Incorrect password provided.", "Failed", user.Id, user.Email);
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (createResult.Succeeded)
            {
                // Ensure role exists
                string chosenRole = model.Role;
                if (!await _roleManager.RoleExistsAsync(chosenRole))
                {
                    chosenRole = "SalesExecutive";
                }

                await _userManager.AddToRoleAsync(user, chosenRole);
                await _auditService.LogAsync("Register", "User", user.Id, null, $"Role: {chosenRole}", $"User {user.FullName} ({user.Email}) registered.", "Success", user.Id, user.Email);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _auditService.LogAsync("Logout", "Auth", user.Id, null, null, "User logged out.", "Success", user.Id, user.Email);
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
