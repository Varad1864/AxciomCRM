using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        [Display(Name = "Customer Code")]
        [StringLength(50)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer Name is required.")]
        [Display(Name = "Customer Name")]
        [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
        public string CustomerName { get; set; } = string.Empty;

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

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? State { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Prospect

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }
    }
}
