using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.EnsureCreatedAsync();

            // 1. Seed Roles
            string[] roles = new[] { "Admin", "Manager", "SalesExecutive" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Users
            var usersToCreate = new (string Email, string Name, string Role, string Password)[]
            {
                ("admin@acxiom.com", "System Administrator", "Admin", "Admin@123"),
                ("manager@acxiom.com", "Sales Manager", "Manager", "Manager@123"),
                ("sales1@acxiom.com", "Sarah Connor", "SalesExecutive", "Sales@123"),
                ("sales2@acxiom.com", "John Smith", "SalesExecutive", "Sales@123")
            };

            var userMap = new Dictionary<string, ApplicationUser>();

            foreach (var item in usersToCreate)
            {
                var existingUser = await userManager.FindByEmailAsync(item.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        FullName = item.Name,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow.AddMonths(-3)
                    };

                    var result = await userManager.CreateAsync(user, item.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, item.Role);
                        userMap[item.Email] = user;
                    }
                }
                else
                {
                    userMap[item.Email] = existingUser;
                }
            }

            // 3. Seed Customers if empty
            if (!await context.Customers.AnyAsync())
            {
                var sales1 = userMap["sales1@acxiom.com"];
                var sales2 = userMap["sales2@acxiom.com"];

                var customers = new List<Customer>
                {
                    new Customer
                    {
                        CustomerCode = "CUST-1001",
                        CustomerName = "Nexus Enterprises",
                        Email = "contact@nexusenterprises.com",
                        Phone = "+91-9876543210",
                        CompanyName = "Nexus Enterprises Pvt Ltd",
                        Address = "Cyber City, Tower B",
                        City = "Gurugram",
                        State = "Haryana",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddDays(-40),
                        CreatedBy = "System Administrator",
                        AssignedTo = sales1.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1002",
                        CustomerName = "Apex Global Technologies",
                        Email = "procurement@apexglobal.io",
                        Phone = "+91-9823012345",
                        CompanyName = "Apex Global Corp",
                        Address = "Shivaji Nagar Tech Park",
                        City = "Pune",
                        State = "Maharashtra",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddDays(-25),
                        CreatedBy = "Sales Manager",
                        AssignedTo = sales2.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1003",
                        CustomerName = "Blue Horizon Logistics",
                        Email = "info@bluehorizon.com",
                        Phone = "+91-9765432198",
                        CompanyName = "Blue Horizon Inc",
                        Address = "Whitefield Main Road",
                        City = "Bengaluru",
                        State = "Karnataka",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddDays(-15),
                        CreatedBy = "Sarah Connor",
                        AssignedTo = sales1.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1004",
                        CustomerName = "Vanguard Financial",
                        Email = "ops@vanguardfin.in",
                        Phone = "+91-9988776655",
                        CompanyName = "Vanguard FinTech",
                        Address = "BKC Complex",
                        City = "Mumbai",
                        State = "Maharashtra",
                        Status = "Inactive",
                        CreatedDate = DateTime.UtcNow.AddDays(-60),
                        CreatedBy = "System Administrator",
                        AssignedTo = sales2.Id
                    }
                };

                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();
            }

            // 4. Seed Leads if empty
            if (!await context.Leads.AnyAsync())
            {
                var sales1 = userMap["sales1@acxiom.com"];
                var sales2 = userMap["sales2@acxiom.com"];

                var leads = new List<Lead>
                {
                    new Lead
                    {
                        LeadCode = "LEAD-2001",
                        LeadName = "Arjun Mehta",
                        Email = "arjun.mehta@solarsolutions.com",
                        Phone = "+91-9123456780",
                        CompanyName = "Solar Solutions India",
                        Source = "Website",
                        Status = "New",
                        Priority = "High",
                        ExpectedValue = 45000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-5),
                        AssignedTo = sales1.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2002",
                        LeadName = "Priya Sharma",
                        Email = "priya.sharma@healthplus.org",
                        Phone = "+91-9234567891",
                        CompanyName = "HealthPlus Networks",
                        Source = "Referral",
                        Status = "Qualified",
                        Priority = "High",
                        ExpectedValue = 75000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-12),
                        AssignedTo = sales1.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2003",
                        LeadName = "Rohit Verma",
                        Email = "rohit.verma@edutech.edu",
                        Phone = "+91-9345678902",
                        CompanyName = "EduTech Horizons",
                        Source = "Cold Call",
                        Status = "Contacted",
                        Priority = "Medium",
                        ExpectedValue = 28000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-8),
                        AssignedTo = sales2.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2004",
                        LeadName = "Kavita Rao",
                        Email = "kavita.rao@fintechcloud.com",
                        Phone = "+91-9456789013",
                        CompanyName = "FinTech Cloud Services",
                        Source = "Campaign",
                        Status = "Converted",
                        Priority = "High",
                        ExpectedValue = 120000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-30),
                        AssignedTo = sales2.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2005",
                        LeadName = "Vikram Patil",
                        Email = "vikram.patil@retailchain.com",
                        Phone = "+91-9567890124",
                        CompanyName = "Prime Retail Chains",
                        Source = "Partner",
                        Status = "Lost",
                        Priority = "Low",
                        ExpectedValue = 15000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-20),
                        AssignedTo = sales1.Id
                    }
                };

                context.Leads.AddRange(leads);
                await context.SaveChangesAsync();
            }

            // 5. Seed Opportunities if empty
            if (!await context.Opportunities.AnyAsync())
            {
                var customers = await context.Customers.ToListAsync();
                var leads = await context.Leads.ToListAsync();
                var sales1 = userMap["sales1@acxiom.com"];
                var sales2 = userMap["sales2@acxiom.com"];

                var opps = new List<Opportunity>
                {
                    new Opportunity
                    {
                        OpportunityName = "Nexus Cloud ERP Modernization",
                        CustomerId = customers.FirstOrDefault(c => c.CustomerCode == "CUST-1001")?.CustomerId,
                        Amount = 150000m,
                        Stage = "Negotiation",
                        Probability = 80,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
                        Status = "Open",
                        CreatedDate = DateTime.UtcNow.AddDays(-25),
                        AssignedTo = sales1.Id,
                        Notes = "Contract finalization stage with CTO."
                    },
                    new Opportunity
                    {
                        OpportunityName = "Apex Global AI Analytics Suite",
                        CustomerId = customers.FirstOrDefault(c => c.CustomerCode == "CUST-1002")?.CustomerId,
                        Amount = 95000m,
                        Stage = "Proposal",
                        Probability = 60,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(35),
                        Status = "Open",
                        CreatedDate = DateTime.UtcNow.AddDays(-15),
                        AssignedTo = sales2.Id,
                        Notes = "Custom proposal sent, awaiting board review."
                    },
                    new Opportunity
                    {
                        OpportunityName = "Blue Horizon Fleet Tracking Platform",
                        CustomerId = customers.FirstOrDefault(c => c.CustomerCode == "CUST-1003")?.CustomerId,
                        Amount = 60000m,
                        Stage = "Won",
                        Probability = 100,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                        Status = "Won",
                        CreatedDate = DateTime.UtcNow.AddDays(-30),
                        AssignedTo = sales1.Id,
                        Notes = "Deal signed and invoice raised."
                    },
                    new Opportunity
                    {
                        OpportunityName = "HealthPlus Telemedicine Integration",
                        LeadId = leads.FirstOrDefault(l => l.LeadCode == "LEAD-2002")?.LeadId,
                        Amount = 75000m,
                        Stage = "Qualification",
                        Probability = 30,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(45),
                        Status = "Open",
                        CreatedDate = DateTime.UtcNow.AddDays(-7),
                        AssignedTo = sales1.Id,
                        Notes = "Technical requirement gathering call completed."
                    },
                    new Opportunity
                    {
                        OpportunityName = "Prime Retail POS Revamp",
                        LeadId = leads.FirstOrDefault(l => l.LeadCode == "LEAD-2005")?.LeadId,
                        Amount = 40000m,
                        Stage = "Lost",
                        Probability = 0,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(-10),
                        Status = "Lost",
                        CreatedDate = DateTime.UtcNow.AddDays(-35),
                        AssignedTo = sales2.Id,
                        Notes = "Competitor selected due to existing vendor relationship."
                    }
                };

                context.Opportunities.AddRange(opps);
                await context.SaveChangesAsync();
            }

            // 6. Seed FollowUps if empty
            if (!await context.FollowUps.AnyAsync())
            {
                var customers = await context.Customers.ToListAsync();
                var leads = await context.Leads.ToListAsync();
                var sales1 = userMap["sales1@acxiom.com"];
                var sales2 = userMap["sales2@acxiom.com"];

                var followups = new List<FollowUp>
                {
                    new FollowUp
                    {
                        RelatedType = "Customer",
                        CustomerId = customers[0].CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(2),
                        FollowUpType = "Meeting",
                        Subject = "Nexus ERP Scope Review & Commercial terms",
                        Status = "Planned",
                        Remarks = "Discuss SLA clauses and payment milestones",
                        AssignedTo = sales1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-2)
                    },
                    new FollowUp
                    {
                        RelatedType = "Lead",
                        LeadId = leads[1].LeadId,
                        FollowUpDate = DateTime.UtcNow.AddDays(3),
                        FollowUpType = "Call",
                        Subject = "HealthPlus Follow-up Call with Chief Medical Officer",
                        Status = "Planned",
                        Remarks = "Walkthrough compliance and HIPAA protocols",
                        AssignedTo = sales1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-1)
                    },
                    new FollowUp
                    {
                        RelatedType = "Customer",
                        CustomerId = customers[1].CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(5),
                        FollowUpType = "Demo",
                        Subject = "Apex Global AI Analytics Dashboard Demonstration",
                        Status = "Planned",
                        Remarks = "Showcase live anomaly detection pipeline",
                        AssignedTo = sales2.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-3)
                    },
                    new FollowUp
                    {
                        RelatedType = "Lead",
                        LeadId = leads[0].LeadId,
                        FollowUpDate = DateTime.UtcNow.AddDays(-1),
                        FollowUpType = "Call",
                        Subject = "Solar Solutions Initial Qualification Check",
                        Status = "Missed",
                        Remarks = "Client was in another meeting; reschedule needed",
                        AssignedTo = sales1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-4)
                    },
                    new FollowUp
                    {
                        RelatedType = "Customer",
                        CustomerId = customers[2].CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(-5),
                        FollowUpType = "Meeting",
                        Subject = "Blue Horizon Onboarding Kickoff",
                        Status = "Completed",
                        Remarks = "Handover to customer success team completed smoothly",
                        AssignedTo = sales1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-10)
                    }
                };

                context.FollowUps.AddRange(followups);
                await context.SaveChangesAsync();
            }

            // 7. Seed Activities if empty
            if (!await context.Activities.AnyAsync())
            {
                var customers = await context.Customers.ToListAsync();
                var leads = await context.Leads.ToListAsync();
                var sales1 = userMap["sales1@acxiom.com"];

                var activities = new List<Activity>
                {
                    new Activity
                    {
                        ActivityType = "Call",
                        Subject = "Initial discovery phone call",
                        Description = "Clarified annual software budget and current pain points.",
                        ActivityDate = DateTime.UtcNow.AddDays(-10),
                        CustomerId = customers[0].CustomerId,
                        AssignedTo = sales1.Id,
                        Status = "Completed"
                    },
                    new Activity
                    {
                        ActivityType = "Email",
                        Subject = "Sent product brochure and case study",
                        Description = "Shared manufacturing case study detailing 35% efficiency boost.",
                        ActivityDate = DateTime.UtcNow.AddDays(-7),
                        CustomerId = customers[0].CustomerId,
                        AssignedTo = sales1.Id,
                        Status = "Completed"
                    },
                    new Activity
                    {
                        ActivityType = "Meeting",
                        Subject = "Architecture Review Session",
                        Description = "Reviewed security compliance and cloud architecture requirements.",
                        ActivityDate = DateTime.UtcNow.AddDays(-3),
                        LeadId = leads[1].LeadId,
                        AssignedTo = sales1.Id,
                        Status = "Completed"
                    }
                };

                context.Activities.AddRange(activities);
                await context.SaveChangesAsync();
            }

            // 8. Seed Audit Logs if empty
            if (!await context.AuditLogs.AnyAsync())
            {
                var admin = userMap["admin@acxiom.com"];
                var manager = userMap["manager@acxiom.com"];

                var logs = new List<AuditLog>
                {
                    new AuditLog
                    {
                        UserId = admin.Id,
                        UserName = admin.Email,
                        Action = "Login",
                        EntityName = "Auth",
                        RecordId = admin.Id,
                        Result = "Success",
                        Details = "Administrator logged into AcxiomCRM portal.",
                        CreatedDate = DateTime.UtcNow.AddDays(-3),
                        IpAddress = "127.0.0.1"
                    },
                    new AuditLog
                    {
                        UserId = manager.Id,
                        UserName = manager.Email,
                        Action = "Create",
                        EntityName = "Lead",
                        RecordId = "LEAD-2002",
                        NewValue = "LeadName: Priya Sharma, Company: HealthPlus Networks",
                        Result = "Success",
                        Details = "New qualified prospect lead captured.",
                        CreatedDate = DateTime.UtcNow.AddDays(-2),
                        IpAddress = "127.0.0.1"
                    },
                    new AuditLog
                    {
                        UserId = admin.Id,
                        UserName = admin.Email,
                        Action = "Role Change",
                        EntityName = "User",
                        RecordId = manager.Id,
                        OldValue = "SalesExecutive",
                        NewValue = "Manager",
                        Result = "Success",
                        Details = "Promoted user to Sales Operations Manager.",
                        CreatedDate = DateTime.UtcNow.AddDays(-10),
                        IpAddress = "127.0.0.1"
                    }
                };

                context.AuditLogs.AddRange(logs);
                await context.SaveChangesAsync();
            }
        }
    }
}
