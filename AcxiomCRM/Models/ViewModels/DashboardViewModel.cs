using System;
using System.Collections.Generic;
using AcxiomCRM.Models;

namespace AcxiomCRM.Models.ViewModels
{
    public class DashboardViewModel
    {
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DateFilter { get; set; } = "AllTime"; // Today, ThisWeek, ThisMonth, AllTime

        // KPI Cards (17.11)
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int PendingFollowUps { get; set; }
        public int OverdueFollowUps { get; set; }
        public int ActiveUsersCount { get; set; }

        // Chart 1: Lead Status Breakdown (17.12)
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusData { get; set; } = new();

        // Chart 2: Opportunity Pipeline by Stage (17.12)
        public List<string> PipelineStageLabels { get; set; } = new();
        public List<decimal> PipelineStageAmounts { get; set; } = new();
        public List<int> PipelineStageCounts { get; set; } = new();

        // Chart 3: Monthly Sales / Won Totals (17.12)
        public List<string> MonthlySalesLabels { get; set; } = new();
        public List<decimal> MonthlySalesValues { get; set; } = new();

        // Quick Lists
        public List<FollowUp> UpcomingFollowUps { get; set; } = new();
        public List<Opportunity> RecentOpportunities { get; set; } = new();
        public List<Lead> RecentLeads { get; set; } = new();
        public List<Activity> RecentActivities { get; set; } = new();
    }
}
