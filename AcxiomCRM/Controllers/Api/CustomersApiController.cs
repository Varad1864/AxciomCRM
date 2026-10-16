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
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAuthorizationService _authService;
        private readonly IAuditService _auditService;

        public CustomersApiController(
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

        // GET: api/customers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerResponseDto>>> GetCustomers([FromQuery] string? search)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _authService.FilterCustomers(_context.Customers.Include(c => c.AssignedUser).AsQueryable(), User, user.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) || c.Phone.Contains(search));
            }

            var list = await query
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new CustomerResponseDto
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Email = c.Email,
                    Phone = c.Phone,
                    CompanyName = c.CompanyName,
                    Address = c.Address,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    AssignedUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerResponseDto>> GetCustomer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var c = await _authService.FilterCustomers(_context.Customers.Include(x => x.AssignedUser), User, user.Id)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (c == null) return NotFound(new { message = $"Customer with ID {id} not found or access denied." });

            return Ok(new CustomerResponseDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                AssignedUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null
            });
        }

        // POST: api/customers
        [HttpPost]
        public async Task<ActionResult<CustomerResponseDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Unique email and phone checks (HTTP 409 Conflict)
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "Customer with this email already exists." });
            }
            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
            {
                return Conflict(new { message = "Customer with this phone already exists." });
            }

            string assignedTo = _authService.IsSalesExecutive(User) ? user.Id : (dto.AssignedTo ?? user.Id);

            var customer = new Customer
            {
                CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
                CustomerName = dto.CustomerName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Status = dto.Status ?? "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = user.FullName ?? user.UserName,
                AssignedTo = assignedTo
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("API Create", "Customer", customer.CustomerId.ToString(), null,
                $"Name: {customer.CustomerName}, Email: {customer.Email}", "Customer created via REST API", "Success", user.Id, user.Email);

            var responseDto = new CustomerResponseDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate
            };

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, responseDto);
        }

        // PUT: api/customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _authService.FilterCustomers(_context.Customers, User, user.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found or access denied." });
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "Another customer with this email already exists." });
            }
            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone))
            {
                return Conflict(new { message = "Another customer with this phone number already exists." });
            }

            customer.CustomerName = dto.CustomerName;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;
            customer.CompanyName = dto.CompanyName;
            customer.Address = dto.Address;
            customer.City = dto.City;
            customer.State = dto.State;
            customer.Status = dto.Status;
            if (!_authService.IsSalesExecutive(User) && !string.IsNullOrEmpty(dto.AssignedTo))
            {
                customer.AssignedTo = dto.AssignedTo;
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("API Update", "Customer", id.ToString(), null, $"Name: {customer.CustomerName}", "Updated via API", "Success", user.Id, user.Email);

            return Ok(new { message = "Customer updated successfully" });
        }

        // DELETE: api/customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var customer = await _authService.FilterCustomers(_context.Customers, User, user.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found or access denied." });
            }

            bool hasOpps = await _context.Opportunities.AnyAsync(o => o.CustomerId == id);
            if (hasOpps)
            {
                customer.Status = "Inactive";
                await _context.SaveChangesAsync();
                return Ok(new { message = "Customer deactivated because opportunities are attached." });
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("API Delete", "Customer", id.ToString(), customer.CustomerName, null, "Deleted via API", "Success", user.Id, user.Email);
            return Ok(new { message = "Customer deleted successfully" });
        }
    }
}
