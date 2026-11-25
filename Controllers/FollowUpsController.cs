using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/followups")]
public class FollowUpsController(AppDbContext db) : ControllerBase
{
    /// Record a call/meeting/etc. Updates the customer's stage, last-contacted and next follow-up in one transaction.
    [HttpPost]
    public async Task<IActionResult> Create(FollowUpCreate d)
    {
        var c = await db.Customers.FindAsync(d.CustomerId); if (c is null) return NotFound();
        if (d.NextFollowUpDate.HasValue && d.NextFollowUpDate.Value.Date < d.FollowUpDate.Date)
            return BadRequest(new { message = "Next follow-up cannot be before this follow-up." });

        var f = new FollowUp { CustomerId = d.CustomerId, AgentId = d.AgentId ?? c.AgentId, FollowUpDate = d.FollowUpDate,
            Type = d.Type, Summary = d.Summary, CustomerResponse = d.CustomerResponse, StageAfter = d.StageAfter,
            NextAction = d.NextAction, NextFollowUpDate = d.NextFollowUpDate?.Date, Remarks = d.Remarks,
            CreatedBy = User.Identity?.Name };
        db.FollowUps.Add(f);
        c.Stage = d.StageAfter; c.LastContactedAt = d.FollowUpDate; c.NextFollowUpDate = d.NextFollowUpDate?.Date;
        await db.SaveChangesAsync();
        return Ok(f);
    }

    [HttpGet("customer/{customerId:int}")]
    public async Task<List<FollowUp>> History(int customerId) =>
        await db.FollowUps.AsNoTracking().Include(f => f.Agent).Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.FollowUpDate).ToListAsync();

    // Work lists: today / upcoming / overdue / no-next / stale
    [HttpGet("worklist/{kind}")]
    public async Task<IActionResult> WorkList(string kind, int days = 7)
    {
        var today = DateTime.Today;
        var q = db.Customers.AsNoTracking().Where(c => !c.IsLost && c.Stage < PipelineStage.Completed);
        q = kind switch
        {
            "today"    => q.Where(c => c.NextFollowUpDate == today),
            "upcoming" => q.Where(c => c.NextFollowUpDate > today && c.NextFollowUpDate <= today.AddDays(days)),
            "overdue"  => q.Where(c => c.NextFollowUpDate < today),
            "no-next"  => q.Where(c => c.NextFollowUpDate == null),
            "stale"    => q.Where(c => (c.LastContactedAt ?? c.CreatedAt) < DateTime.UtcNow.AddDays(-days)),
            _ => null!
        };
        if (q is null) return BadRequest();
        var rows = await q.OrderBy(c => c.NextFollowUpDate).Take(500).Select(c => new {
            c.Id, c.CustomerCode, c.CompanyName, c.ContactPerson, c.Mobile, Agent = c.Agent!.Name,
            Stage = c.Stage.ToString(), c.NextFollowUpDate, c.LastContactedAt,
            DaysUnattended = (int)(DateTime.UtcNow - (c.LastContactedAt ?? c.CreatedAt)).TotalDays }).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("daily-report")]
    public async Task<IActionResult> Daily(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date; var e = d.AddDays(1);
        var day = db.FollowUps.Where(f => f.FollowUpDate >= d && f.FollowUpDate < e);
        return Ok(new {
            Date = d,
            Calls = await day.CountAsync(f => f.Type == FollowUpType.Call || f.Type == FollowUpType.WhatsApp || f.Type == FollowUpType.Email),
            Meetings = await day.CountAsync(f => f.Type == FollowUpType.Meeting),
            SiteVisits = await day.CountAsync(f => f.Type == FollowUpType.SiteVisit),
            NewEnquiries = await db.Customers.CountAsync(c => c.EnquiryDate == d),
            Completed = await day.CountAsync(),
            Pending = await db.Customers.CountAsync(c => !c.IsLost && c.NextFollowUpDate == d),
            Overdue = await db.Customers.CountAsync(c => !c.IsLost && c.Stage < PipelineStage.Completed && c.NextFollowUpDate < d)
        });
    }
}
