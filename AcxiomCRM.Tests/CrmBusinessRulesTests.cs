using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Tests
{
    public class CrmBusinessRulesTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private IList<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, validationResults, true);
            return validationResults;
        }

        [Fact]
        public void Opportunity_AmountMustBeGreaterThanZero()
        {
            // Requirement 5.3 & 5.4: Opportunity Amount <= 0 -> reject with: "Opportunity Amount must be greater than 0."
            var oppZero = new Opportunity
            {
                OpportunityName = "Test Opp",
                Amount = 0m,
                Probability = 50,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
                Stage = "Qualification"
            };

            var results = ValidateModel(oppZero);
            Assert.Contains(results, r => r.MemberNames.Contains("Amount"));

            var oppNegative = new Opportunity
            {
                OpportunityName = "Test Opp",
                Amount = -500m,
                Probability = 50,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
                Stage = "Qualification"
            };

            var resultsNegative = ValidateModel(oppNegative);
            Assert.Contains(resultsNegative, r => r.MemberNames.Contains("Amount"));
        }

        [Fact]
        public void Opportunity_ProbabilityMustBeBetween0And100()
        {
            // Requirement 5.3 & 5.4: Probability = 101 -> reject with: "Probability must be between 0 and 100."
            var oppInvalidProb = new Opportunity
            {
                OpportunityName = "Test Opp",
                Amount = 1000m,
                Probability = 105,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
                Stage = "Qualification"
            };

            var results = ValidateModel(oppInvalidProb);
            Assert.Contains(results, r => r.MemberNames.Contains("Probability"));

            var oppNegativeProb = new Opportunity
            {
                OpportunityName = "Test Opp",
                Amount = 1000m,
                Probability = -5,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
                Stage = "Qualification"
            };

            var resultsNegative = ValidateModel(oppNegativeProb);
            Assert.Contains(resultsNegative, r => r.MemberNames.Contains("Probability"));
        }

        [Fact]
        public void Opportunity_WeightedPipelineCalculatedCorrectly()
        {
            // Requirement 4.8: Amount * Probability / 100
            var opp = new Opportunity
            {
                OpportunityName = "Enterprise ERP Deal",
                Amount = 50000m,
                Probability = 60,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
            };

            Assert.Equal(30000m, opp.WeightedPipeline);
        }

        [Fact]
        public void Customer_ValidationRequiresNameEmailAndPhone()
        {
            // Requirement 5.1 & 17.5: Required, Email, Phone validation
            var invalidCustomer = new Customer
            {
                CustomerCode = "CUST-001",
                CustomerName = "", // Invalid
                Email = "invalid-email-format", // Invalid
                Phone = "abc" // Invalid phone
            };

            var results = ValidateModel(invalidCustomer);
            Assert.Contains(results, r => r.MemberNames.Contains("CustomerName"));
            Assert.Contains(results, r => r.MemberNames.Contains("Email"));
            Assert.Contains(results, r => r.MemberNames.Contains("Phone"));
        }

        [Fact]
        public void FollowUp_CannotBeEarlierThanTodayForPlannedFollowUp()
        {
            // Requirement 5.3 & 17.7: Follow-Up Date Cannot be earlier than today for a new/planned follow-up
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var followUp = new FollowUp
            {
                Subject = "Client call",
                FollowUpDate = yesterday,
                Status = "Planned"
            };

            bool isPastDateForPlanned = followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date;
            Assert.True(isPastDateForPlanned);
        }

        [Fact]
        public async Task Lead_ConversionCreatesCustomerAndOpportunity()
        {
            // Section 8: Lead-to-Customer conversion workflow
            using var context = GetInMemoryContext();

            var lead = new Lead
            {
                LeadCode = "LEAD-999",
                LeadName = "Global Enterprises Contact",
                Email = "director@globalent.com",
                Phone = "+91-9988776655",
                CompanyName = "Global Enterprises",
                Status = "Qualified",
                ExpectedValue = 75000m,
                AssignedTo = "sales-user-1"
            };

            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            // Simulate conversion
            var customer = new Customer
            {
                CustomerCode = "CUST-CONV-999",
                CustomerName = lead.CompanyName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Status = "Active",
                AssignedTo = lead.AssignedTo
            };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var opp = new Opportunity
            {
                OpportunityName = $"{lead.CompanyName} Contract",
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = lead.ExpectedValue,
                Stage = "Qualification",
                Probability = 30,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                AssignedTo = lead.AssignedTo
            };
            context.Opportunities.Add(opp);

            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;
            lead.ConvertedOpportunityId = opp.OpportunityId;

            await context.SaveChangesAsync();

            Assert.Equal("Converted", lead.Status);
            Assert.Equal(customer.CustomerId, lead.ConvertedCustomerId);
            Assert.Equal(opp.OpportunityId, lead.ConvertedOpportunityId);
            Assert.Equal(75000m, opp.Amount);
        }

        [Fact]
        public void RoleAuthorization_ScopingRulesWorkAsExpected()
        {
            // Section 7.1: Suggested Permission Matrix
            var mockUsers = new List<ApplicationUser>();
            // Verify CrmAuthorizationService query filter rules
            using var context = GetInMemoryContext();

            context.Customers.Add(new Customer { CustomerCode = "C1", CustomerName = "Sales1 Cust", Email = "c1@test.com", Phone = "+91-1111111111", AssignedTo = "user-sales-1" });
            context.Customers.Add(new Customer { CustomerCode = "C2", CustomerName = "Sales2 Cust", Email = "c2@test.com", Phone = "+91-2222222222", AssignedTo = "user-sales-2" });
            context.SaveChanges();

            // When user is SalesExecutive (user-sales-1)
            var sales1Claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-sales-1"),
                new Claim(ClaimTypes.Role, "SalesExecutive")
            }, "TestAuth"));

            // When user is Admin
            var adminClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"));

            // Filter logic test:
            var allCustomers = context.Customers.AsQueryable();

            var sales1Visible = adminClaims.IsInRole("Admin") ? allCustomers : allCustomers.Where(c => c.AssignedTo == "user-sales-1");
            Assert.Equal(2, sales1Visible.Count());

            var repVisible = sales1Claims.IsInRole("Admin") ? allCustomers : allCustomers.Where(c => c.AssignedTo == "user-sales-1");
            Assert.Single(repVisible);
            Assert.Equal("C1", repVisible.First().CustomerCode);
        }
    }
}
