using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerRequirements> Requirements => Set<CustomerRequirements>();
    public DbSet<MidcLocation> MidcLocations => Set<MidcLocation>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DocumentFile> Documents => Set<DocumentFile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c) =>
        c.Properties<decimal>().HavePrecision(14, 2);

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Columns computed by SQL Server (see database/01_schema.sql) - EF must never write them.
        b.Entity<Agent>().Property(x => x.AgentCode).HasComputedColumnSql("''", stored: true);
        b.Entity<Customer>().Property(x => x.CustomerCode).HasComputedColumnSql("''", stored: true);
        var q = b.Entity<Quotation>();
        q.Property(x => x.QuotationNo).HasComputedColumnSql("''", stored: true);
        q.Property(x => x.PlotValue).HasComputedColumnSql("0", stored: true);
        q.Property(x => x.ConsultancyFee).HasComputedColumnSql("0", stored: true);
        q.Property(x => x.GstAmount).HasComputedColumnSql("0", stored: true);
        q.Property(x => x.GrandTotal).HasComputedColumnSql("0", stored: true);

        b.Entity<Customer>().Property(x => x.EnquiryDate).HasColumnType("date");
        b.Entity<Customer>().Property(x => x.NextFollowUpDate).HasColumnType("date");
        b.Entity<FollowUp>().Property(x => x.NextFollowUpDate).HasColumnType("date");
        b.Entity<Quotation>().Property(x => x.QuotationDate).HasColumnType("date");
        b.Entity<Quotation>().Property(x => x.ValidUntil).HasColumnType("date");
        b.Entity<Payment>().Property(x => x.PaymentDate).HasColumnType("date");
        b.Entity<Agent>().Property(x => x.JoinDate).HasColumnType("date");
        b.Entity<Plot>().Property(x => x.Category).HasMaxLength(20);

        b.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Customer>().HasMany(c => c.Requirements).WithOne().HasForeignKey(r => r.CustomerId);
        b.Entity<Customer>().HasMany(c => c.FollowUps).WithOne(f => f.Customer).HasForeignKey(f => f.CustomerId);
        b.Entity<Quotation>().HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.QuotationId);
        b.Entity<AuditLog>().ToTable("AuditLogs");
        b.Entity<DocumentFile>().ToTable("Documents");
        b.Entity<CustomerRequirements>().ToTable("CustomerRequirements");
    }
}
