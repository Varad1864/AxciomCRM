using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.DTOs
{
    public class FollowUpResponseDto
    {
        public int FollowUpId { get; set; }
        public string RelatedType { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public DateTime FollowUpDate { get; set; }
        public string FollowUpType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public class CreateFollowUpDto
    {
        [Required]
        public string RelatedType { get; set; } = "Customer";

        public int? CustomerId { get; set; }
        public int? LeadId { get; set; }

        [Required]
        public DateTime FollowUpDate { get; set; }

        [Required]
        public string FollowUpType { get; set; } = "Call";

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        public string Status { get; set; } = "Planned";
        public string? Remarks { get; set; }
        public string? AssignedTo { get; set; }
    }

    public class PipelineReportDto
    {
        public decimal TotalPipelineValue { get; set; }
        public decimal TotalWeightedValue { get; set; }
        public int TotalOpportunitiesCount { get; set; }
        public List<StageSummaryDto> StageSummaries { get; set; } = new();
        public List<OwnerSummaryDto> OwnerSummaries { get; set; } = new();
    }

    public class StageSummaryDto
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class OwnerSummaryDto
    {
        public string OwnerName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ApiLoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class ApiLoginResponseDto
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }
}
