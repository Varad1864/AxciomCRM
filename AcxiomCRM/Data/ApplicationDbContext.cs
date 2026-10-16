using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<FollowUp> FollowUps => Set<FollowUp>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customer unique constraints
            builder.Entity<Customer>()
                .HasIndex(c => c.Email)
                .IsUnique();

            builder.Entity<Customer>()
                .HasIndex(c => c.Phone)
                .IsUnique();

            builder.Entity<Customer>()
                .HasIndex(c => c.CustomerCode)
                .IsUnique();

            // Lead unique constraints
            builder.Entity<Lead>()
                .HasIndex(l => l.LeadCode)
                .IsUnique();

            // Customer relationships
            builder.Entity<Customer>()
                .HasOne(c => c.AssignedUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // Lead relationships
            builder.Entity<Lead>()
                .HasOne(l => l.AssignedUser)
                .WithMany()
                .HasForeignKey(l => l.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // Opportunity relationships
            builder.Entity<Opportunity>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Opportunity>()
                .HasOne(o => o.Lead)
                .WithMany()
                .HasForeignKey(o => o.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Opportunity>()
                .HasOne(o => o.AssignedUser)
                .WithMany()
                .HasForeignKey(o => o.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // FollowUp relationships
            builder.Entity<FollowUp>()
                .HasOne(f => f.Customer)
                .WithMany()
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FollowUp>()
                .HasOne(f => f.Lead)
                .WithMany()
                .HasForeignKey(f => f.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FollowUp>()
                .HasOne(f => f.AssignedUser)
                .WithMany()
                .HasForeignKey(f => f.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // Activity relationships
            builder.Entity<Activity>()
                .HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Activity>()
                .HasOne(a => a.Lead)
                .WithMany()
                .HasForeignKey(a => a.LeadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Activity>()
                .HasOne(a => a.AssignedUser)
                .WithMany()
                .HasForeignKey(a => a.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // Audit log indexing
            builder.Entity<AuditLog>()
                .HasIndex(a => a.CreatedDate);

            builder.Entity<AuditLog>()
                .HasIndex(a => a.EntityName);
        }
    }
}
