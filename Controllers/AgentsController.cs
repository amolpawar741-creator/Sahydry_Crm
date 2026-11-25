using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/agents")]
public class AgentsController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<List<Agent>> List(bool activeOnly = false) =>
        await db.Agents.Where(a => !activeOnly || a.IsActive).OrderBy(a => a.Name).ToListAsync();

    [HttpGet("{id:int}")] public async Task<ActionResult<Agent>> Get(int id) =>
        await db.Agents.FindAsync(id) is { } a ? a : NotFound();

    [HttpPost] public async Task<ActionResult<Agent>> Create(Agent a)
    {
        a.Id = 0; db.Agents.Add(a); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = a.Id }, a);
    }

    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, Agent a)
    {
        var e = await db.Agents.FindAsync(id); if (e is null) return NotFound();
        e.Name = a.Name; e.Mobile = a.Mobile; e.Email = a.Email; e.Address = a.Address; e.Region = a.Region;
        e.CommissionTerms = a.CommissionTerms; e.IsActive = a.IsActive;     // deactivate instead of delete
        await db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("performance")] public async Task<List<AgentPerformanceRow>> Performance(DateTime? from, DateTime? to) =>
        await AgentStats.Build(db, from, to);
}

public static class AgentStats
{
    public static async Task<List<AgentPerformanceRow>> Build(AppDbContext db, DateTime? from = null, DateTime? to = null)
    {
        var cust = db.Customers.AsQueryable();
        var fu = db.FollowUps.AsQueryable();
        if (from.HasValue) { cust = cust.Where(c => c.EnquiryDate >= from); fu = fu.Where(f => f.FollowUpDate >= from); }
        if (to.HasValue)   { cust = cust.Where(c => c.EnquiryDate <= to);   fu = fu.Where(f => f.FollowUpDate <= to); }

        var c1 = await cust.Where(c => c.AgentId != null).GroupBy(c => c.AgentId!.Value).Select(g => new {
            Id = g.Key, Total = g.Count(),
            Active = g.Count(c => !c.IsLost && c.Stage < PipelineStage.Completed),
            Done = g.Count(c => !c.IsLost && c.Stage == PipelineStage.Completed) }).ToDictionaryAsync(x => x.Id);
        var f1 = await fu.Where(f => f.AgentId != null).GroupBy(f => f.AgentId!.Value).Select(g => new {
            Id = g.Key,
            Calls = g.Count(f => f.Type == FollowUpType.Call || f.Type == FollowUpType.WhatsApp || f.Type == FollowUpType.Email),
            Meet = g.Count(f => f.Type == FollowUpType.Meeting),
            Site = g.Count(f => f.Type == FollowUpType.SiteVisit) }).ToDictionaryAsync(x => x.Id);
        var rev = await db.Payments.Join(db.Quotations, p => p.QuotationId, q => q.Id, (p, q) => new { p.Amount, q.CustomerId })
            .Join(db.Customers, x => x.CustomerId, c => c.Id, (x, c) => new { x.Amount, c.AgentId })
            .Where(x => x.AgentId != null).GroupBy(x => x.AgentId!.Value)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id);

        var agents = await db.Agents.Where(a => a.IsActive).ToListAsync();
        return agents.Select(a => new AgentPerformanceRow(a.Id, a.AgentCode, a.Name,
            c1.GetValueOrDefault(a.Id)?.Total ?? 0, c1.GetValueOrDefault(a.Id)?.Active ?? 0, c1.GetValueOrDefault(a.Id)?.Done ?? 0,
            f1.GetValueOrDefault(a.Id)?.Calls ?? 0, f1.GetValueOrDefault(a.Id)?.Meet ?? 0, f1.GetValueOrDefault(a.Id)?.Site ?? 0,
            rev.GetValueOrDefault(a.Id)?.Sum ?? 0)).OrderByDescending(r => r.TotalEnquiries).ToList();
    }
}
