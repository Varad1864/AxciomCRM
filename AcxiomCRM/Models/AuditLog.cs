using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        [StringLength(100)]
        public string? UserId { get; set; }

        [StringLength(150)]
        [Display(Name = "User Name / Email")]
        public string? UserName { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // Login, Logout, Failed Login, Create, Update, Delete, Role Change, Security, Convert

        [Required]
        [StringLength(50)]
        [Display(Name = "Module / Entity")]
        public string EntityName { get; set; } = string.Empty; // Customer, Lead, Opportunity, FollowUp, User, Auth

        [StringLength(50)]
        [Display(Name = "Record ID")]
        public string? RecordId { get; set; }

        [Display(Name = "Old Value")]
        public string? OldValue { get; set; }

        [Display(Name = "New Value")]
        public string? NewValue { get; set; }

        [Required]
        [Display(Name = "Timestamp")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        [Display(Name = "IP Address")]
        public string? IpAddress { get; set; }

        [StringLength(50)]
        public string? Result { get; set; } = "Success"; // Success, Failed, Warning

        [StringLength(1000)]
        public string? Details { get; set; }
    }
}
