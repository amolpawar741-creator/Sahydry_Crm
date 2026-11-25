using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Get()
    {
        var today = DateTime.Today; var monthStart = new DateTime(today.Year, today.Month, 1);
        var active = db.Customers.Where(c => !c.IsLost && c.Stage < PipelineStage.Completed);

        var revenue = await db.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0;
        var billed = await db.Quotations.SumAsync(q => (decimal?)q.GrandTotal) ?? 0;
        var advances = await db.Quotations.SumAsync(q => (decimal?)q.AdvanceAmount) ?? 0;

        var trend = await db.Customers.Where(c => c.EnquiryDate >= monthStart.AddMonths(-5))
            .GroupBy(c => new { c.EnquiryDate.Year, c.EnquiryDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();

        //var demand = await db.Requirements.Where(r => r.IsActive && r.PreferredLocationId != null)
        //    .Join(db.MidcLocations, r => r.PreferredLocationId, l => l.Id, (r, l) => l.Name)
        //    .GroupBy(n => n).Select(g => new LabelValue(g.Key, g.Count())).OrderByDescending(x => x.Value).Take(8).ToListAsync();

        var demand = await db.Customers.Where(c => c.EnquiryDate >= monthStart.AddMonths(-5))
            .GroupBy(c => new { c.EnquiryDate.Year, c.EnquiryDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();
        //await db.Requirements.Where(r => r.IsActive && r.PreferredLocationId != null)
        //.Join(db.MidcLocations, r => r.PreferredLocationId, l => l.Id, (r, l) => l.Name)
        //.GroupBy(n => n).Select(g => new LabelValue(g.Key, g.Count())).OrderByDescending(x => x.Value).Take(8).ToListAsync();


        return new DashboardDto(
            TotalAgents: await db.Agents.CountAsync(a => a.IsActive),
            TotalCustomers: await db.Customers.CountAsync(),
            NewThisMonth: await db.Customers.CountAsync(c => c.EnquiryDate >= monthStart),
            ActiveCustomers: await active.CountAsync(),
            ActiveRequirements: await db.Requirements.CountAsync(r => r.IsActive),
            TodayFollowUps: await active.CountAsync(c => c.NextFollowUpDate == today),
            OverdueFollowUps: await active.CountAsync(c => c.NextFollowUpDate < today),
            MeetingsThisMonth: await db.FollowUps.CountAsync(f => f.Type == FollowUpType.Meeting && f.FollowUpDate >= monthStart),
            SiteVisitsThisMonth: await db.FollowUps.CountAsync(f => f.Type == FollowUpType.SiteVisit && f.FollowUpDate >= monthStart),
            Quotations: await db.Quotations.CountAsync(),
            MousSigned: await db.Customers.CountAsync(c => !c.IsLost && c.Stage >= PipelineStage.MouSigned),
            ApplicationsSubmitted: await db.Customers.CountAsync(c => !c.IsLost && c.Stage >= PipelineStage.ApplicationSubmitted),
            PlotsAllotted: await db.Customers.CountAsync(c => !c.IsLost && c.Stage >= PipelineStage.Allotted),
            Revenue: revenue + advances,
            Outstanding: billed - advances - revenue,
            Agents: (await AgentStats.Build(db)).Take(10).ToList(),
            EnquiryTrend: trend.Select(t => new LabelValue($"{new DateTime(t.Year, t.Month, 1):MMM yy}", t.N)).ToList(),
            MidcDemand: demand.Select(t => new LabelValue($"{new DateTime(t.Year, t.Month, 1):MMM yy}", t.N)).ToList());
    }
}
