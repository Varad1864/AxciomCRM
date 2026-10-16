using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Activity
    {
        [Key]
        public int ActivityId { get; set; }

        [Required(ErrorMessage = "Activity Type is required.")]
        [StringLength(50)]
        [Display(Name = "Activity Type")]
        public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Activity Date")]
        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [Display(Name = "Lead")]
        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead? Lead { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Completed"; // Pending, In Progress, Completed
    }
}
