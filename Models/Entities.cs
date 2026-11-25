using System.ComponentModel.DataAnnotations;
namespace ShaydreCrm.Api.Models;

public enum PipelineStage : byte
{
    NewEnquiry = 1, Contacted, Interested, RequirementConfirmed, PlotIdentification, PlotShortlisted,
    SiteVisit, QuotationSubmitted, Negotiation, MouSigned, MillapRegistration, ApplicationSubmitted,
    MidcProcessing, Allotted, Completed
}
public enum FollowUpType : byte { Call = 1, WhatsApp, Meeting, Email, SiteVisit }
public enum PlotStatus : byte { Vacant = 1, AvailableForApplication, UnderProcess, Reserved, Allotted, NotAvailable, VerificationPending }

public class AdminUser
{
    public int Id { get; set; }
    [MaxLength(60)] public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(120)] public string FullName { get; set; } = "";
    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;
}

public class Agent
{
    public int Id { get; set; }
    public string AgentCode { get; set; } = "";          // computed in DB
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(15)] public string Mobile { get; set; } = "";
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
    public string? CommissionTerms { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = "";       // computed in DB
    [Required, MaxLength(200)] public string CompanyName { get; set; } = "";
    [Required, MaxLength(120)] public string ContactPerson { get; set; } = "";
    public string? Designation { get; set; }
    [Required, MaxLength(15)] public string Mobile { get; set; } = "";
    public string? AltMobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? State { get; set; }
    public string? IndustryType { get; set; }
    [MaxLength(15)] public string? GstNumber { get; set; }
    [MaxLength(10)] public string? PanNumber { get; set; }
    public string? Website { get; set; }
    public string? LeadSource { get; set; }
    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }
    public DateTime EnquiryDate { get; set; } = DateTime.Today;
    public PipelineStage Stage { get; set; } = PipelineStage.NewEnquiry;
    public bool IsLost { get; set; }
    public string? LostReason { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CustomerRequirements> Requirements { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
}

public class MidcLocation
{
    public int Id { get; set; }
    public string State { get; set; } = "Maharashtra";
    [Required] public string District { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public string? Phase { get; set; }
    public string? IndustrialArea { get; set; }
    public string? RegionalOffice { get; set; }
    public string? ContactDetails { get; set; }
}

public class Plot
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public MidcLocation? Location { get; set; }
    [Required] public string PlotNumber { get; set; } = "";
    public string? PlotSeries { get; set; }
    public decimal AreaSqm { get; set; }
    [Required] public string Category { get; set; } = "Industrial";
    public decimal RatePerSqm { get; set; }
    public PlotStatus Status { get; set; } = PlotStatus.VerificationPending;
    public bool IsOfficiallyVerified { get; set; }
    public string? LayoutUrl { get; set; }
    public string? Remarks { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class CustomerRequirements
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? RequiredState { get; set; }
    public string? RequiredDistrict { get; set; }
    public int? PreferredLocationId { get; set; }
    public string PlotCategory { get; set; } = "Industrial";
    public decimal RequiredAreaSqm { get; set; }
    public decimal? MinAreaSqm { get; set; }
    public decimal? MaxAreaSqm { get; set; }
    public decimal? Budget { get; set; }
    public decimal? PreferredRate { get; set; }
    public string? BusinessActivity { get; set; }
    public string? OperationType { get; set; }
    public string? Timeline { get; set; }
    public string? SpecialNotes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FollowUp
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }
    public DateTime FollowUpDate { get; set; } = DateTime.Now;
    public FollowUpType Type { get; set; }
    [Required] public string Summary { get; set; } = "";
    public string? CustomerResponse { get; set; }
    public PipelineStage StageAfter { get; set; }
    public string? NextAction { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Quotation
{
    public int Id { get; set; }
    public string QuotationNo { get; set; } = "";        // computed
    public DateTime QuotationDate { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? PlotId { get; set; }
    public Plot? Plot { get; set; }
    public decimal AreaSqm { get; set; }
    public decimal MidcRate { get; set; }
    public decimal ConsultancyRate { get; set; }
    public decimal PlotValue { get; set; }               // computed: Area x MidcRate
    public decimal ConsultancyFee { get; set; }          // computed: Area x ConsultancyRate
    public decimal GstPercent { get; set; } = 18;
    public decimal GstAmount { get; set; }               // computed
    public decimal GrandTotal { get; set; }              // computed: Fee + GST
    public decimal AdvanceAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Remarks { get; set; }
    public List<Payment> Payments { get; set; } = new();
}

public class Payment
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    [Required] public string Mode { get; set; } = "UPI";
    public string? Reference { get; set; }
}

public class DocumentFile
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string DocType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserName { get; set; }
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
