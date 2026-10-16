using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Opportunity
    {
        [Key]
        public int OpportunityId { get; set; }

        [Required(ErrorMessage = "Opportunity Name is required.")]
        [Display(Name = "Opportunity Name")]
        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        public string OpportunityName { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [Display(Name = "Related Lead")]
        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead? Lead { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 1000000000.0, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount ($)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Pipeline Stage is required.")]
        [StringLength(50)]
        public string Stage { get; set; } = "Qualification"; // Qualification, Proposal, Negotiation, Won, Lost

        [Required(ErrorMessage = "Probability is required.")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Probability (%)")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Open"; // Open, Won, Lost

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Assigned To / Owner")]
        public string? AssignedTo { get; set; }

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [NotMapped]
        [Display(Name = "Weighted Pipeline Value")]
        public decimal WeightedPipeline => (Amount * Probability) / 100m;
    }
}
