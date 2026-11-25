namespace ShaydreCrm.Api.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string FullName, DateTime ExpiresAt);
public record StageChange(PipelineStage Stage, bool IsLost, string? LostReason);
public record FollowUpCreate(int CustomerId, int? AgentId, DateTime FollowUpDate, FollowUpType Type, string Summary,
    string? CustomerResponse, PipelineStage StageAfter, string? NextAction, DateTime? NextFollowUpDate, string? Remarks);
public record QuotationCreate(int CustomerId, int? PlotId, decimal AreaSqm, decimal MidcRate, decimal ConsultancyRate,
    decimal GstPercent, decimal AdvanceAmount, string? PaymentTerms, DateTime? ValidUntil, string? Remarks);
public record PaymentCreate(DateTime PaymentDate, decimal Amount, string Mode, string? Reference);
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record CustomerRow(int Id, string CustomerCode, string CompanyName, string ContactPerson, string Mobile,
    string? AgentName, PipelineStage Stage, bool IsLost, DateTime? NextFollowUpDate, DateTime? LastContactedAt);
public record AgentPerformanceRow(int AgentId, string AgentCode, string Name, int TotalEnquiries, int ActiveCustomers,
    int CompletedDeals, int Calls, int Meetings, int SiteVisits, decimal Revenue);
public record DashboardDto(int TotalAgents, int TotalCustomers, int NewThisMonth, int ActiveCustomers, int ActiveRequirements,
    int TodayFollowUps, int OverdueFollowUps, int MeetingsThisMonth, int SiteVisitsThisMonth, int Quotations,
    int MousSigned, int ApplicationsSubmitted, int PlotsAllotted, decimal Revenue, decimal Outstanding,
    List<AgentPerformanceRow> Agents, List<LabelValue> EnquiryTrend, List<LabelValue> MidcDemand);
public record LabelValue(string Label, int Value);
