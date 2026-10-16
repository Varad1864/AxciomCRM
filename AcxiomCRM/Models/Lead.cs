using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Lead
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        [Display(Name = "Lead Code")]
        [StringLength(50)]
        public string LeadCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lead Name is required.")]
        [Display(Name = "Lead Name")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[0-9+\-\s()]{7,15}$", ErrorMessage = "Please enter a valid phone number (7-15 digits).")]
        [Display(Name = "Phone Number")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Company Name")]
        [StringLength(100)]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Lead Source is required.")]
        [StringLength(50)]
        public string Source { get; set; } = "Website"; // Website, Referral, Cold Call, Campaign, Partner, Other

        [Required(ErrorMessage = "Lead Status is required.")]
        [StringLength(50)]
        public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

        [Required]
        [StringLength(30)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        [Required(ErrorMessage = "Expected Value is required.")]
        [Range(0, 100000000, ErrorMessage = "Expected Value must be greater than or equal to 0.")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Expected Value ($)")]
        public decimal ExpectedValue { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }

        // Optional link if converted
        public int? ConvertedCustomerId { get; set; }
        public int? ConvertedOpportunityId { get; set; }
    }
}
