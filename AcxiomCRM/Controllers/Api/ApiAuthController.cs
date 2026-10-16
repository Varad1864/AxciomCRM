using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class ApiAuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ApiAuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                await _auditService.LogAsync("API Login Failed", "Auth", null, null, null, $"Failed API login for {request.Email}", "Failed");
                return Unauthorized(new { message = "Invalid email or inactive user." });
            }

            var result = await _signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync("API Login Success", "Auth", user.Id, null, null, $"API login successful for {user.Email}", "Success", user.Id, user.Email);

                return Ok(new ApiLoginResponseDto
                {
                    Succeeded = true,
                    Message = "Authentication successful",
                    UserId = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Role = roles.Count > 0 ? roles[0] : "SalesExecutive"
                });
            }

            if (result.IsLockedOut)
            {
                return StatusCode(423, new { message = "User account locked due to excessive failed attempts." });
            }

            await _auditService.LogAsync("API Login Failed", "Auth", user.Id, null, null, "Incorrect password via API", "Failed");
            return Unauthorized(new { message = "Invalid credentials." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Logged out successfully" });
        }
    }
}
