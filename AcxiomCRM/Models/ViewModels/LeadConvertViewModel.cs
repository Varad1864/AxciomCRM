using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.ViewModels
{
    public class LeadConvertViewModel
    {
        public int LeadId { get; set; }

        [Display(Name = "Lead Code")]
        public string LeadCode { get; set; } = string.Empty;

        [Display(Name = "Lead Name")]
        public string LeadName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Company")]
        public string? CompanyName { get; set; }

        [Display(Name = "Create Customer Record")]
        public bool CreateCustomer { get; set; } = true;

        [Display(Name = "Customer Code")]
        public string CustomerCode { get; set; } = string.Empty;

        [Display(Name = "Customer Name")]
        [Required(ErrorMessage = "Customer Name is required when converting.")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Create Sales Opportunity")]
        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Name")]
        public string? OpportunityName { get; set; }

        [Display(Name = "Opportunity Amount ($)")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal OpportunityAmount { get; set; }

        [Display(Name = "Pipeline Stage")]
        public string OpportunityStage { get; set; } = "Qualification";

        [Display(Name = "Probability (%)")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int OpportunityProbability { get; set; } = 30;

        [Display(Name = "Expected Close Date")]
        public DateTime OpportunityCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Display(Name = "Assign To")]
        public string? AssignedTo { get; set; }

        public string? Notes { get; set; }
    }
}
