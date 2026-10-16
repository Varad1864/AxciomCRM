using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.DTOs
{
    public class OpportunityResponseDto
    {
        public int OpportunityId { get; set; }
        public string OpportunityName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public decimal Amount { get; set; }
        public string Stage { get; set; } = string.Empty;
        public int Probability { get; set; }
        public decimal WeightedPipeline { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? AssignedUserName { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateOpportunityDto
    {
        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(150)]
        public string OpportunityName { get; set; } = string.Empty;

        public int? CustomerId { get; set; }
        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Pipeline Stage is required.")]
        public string Stage { get; set; } = "Qualification";

        [Required(ErrorMessage = "Probability is required.")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        public DateTime ExpectedCloseDate { get; set; }

        public string? AssignedTo { get; set; }
        public string? Notes { get; set; }
    }
}
