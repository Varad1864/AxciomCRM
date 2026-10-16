using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.DTOs
{
    public class CustomerResponseDto
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public class CreateCustomerDto
    {
        [Required(ErrorMessage = "Customer Name is required.")]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Valid email address required.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[0-9+\-\s()]{7,15}$", ErrorMessage = "Valid phone number required.")]
        public string Phone { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string Status { get; set; } = "Active";
        public string? AssignedTo { get; set; }
    }

    public class UpdateCustomerDto
    {
        [Required(ErrorMessage = "Customer Name is required.")]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[0-9+\-\s()]{7,15}$")]
        public string Phone { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string Status { get; set; } = "Active";
        public string? AssignedTo { get; set; }
    }
}
