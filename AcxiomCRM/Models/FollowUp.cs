using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class FollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Related Type")]
        public string RelatedType { get; set; } = "Customer"; // Customer, Lead, Opportunity

        public int? RelatedId { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [Display(Name = "Lead")]
        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead? Lead { get; set; }

        [Required(ErrorMessage = "Follow-up date and time is required.")]
        [Display(Name = "Follow-up Date")]
        public DateTime FollowUpDate { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required(ErrorMessage = "Follow-up type is required.")]
        [StringLength(50)]
        [Display(Name = "Activity / Type")]
        public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Demo

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

        [StringLength(1000)]
        [Display(Name = "Notes / Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
