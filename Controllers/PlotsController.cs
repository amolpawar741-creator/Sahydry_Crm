using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api")]
public class PlotsController(AppDbContext db) : ControllerBase
{
    [HttpGet("locations")] public async Task<List<MidcLocation>> Locations(string? district) =>
        await db.MidcLocations.Where(l => district == null || l.District == district).OrderBy(l => l.Name).ToListAsync();

    [HttpPost("locations")] public async Task<MidcLocation> AddLocation(MidcLocation l)
    { l.Id = 0; db.MidcLocations.Add(l); await db.SaveChangesAsync(); return l; }

    [HttpGet("plots")]
    public async Task<IActionResult> Search(int? locationId, string? district, PlotStatus? status, string? category,
        decimal? minArea, decimal? maxArea, string? plotNumber)
    {
        var q = db.Plots.AsNoTracking().Include(p => p.Location).AsQueryable();
        if (locationId.HasValue) q = q.Where(p => p.LocationId == locationId);
        if (!string.IsNullOrEmpty(district)) q = q.Where(p => p.Location!.District == district);
        if (status.HasValue) q = q.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(category)) q = q.Where(p => p.Category == category);
        if (minArea.HasValue) q = q.Where(p => p.AreaSqm >= minArea);
        if (maxArea.HasValue) q = q.Where(p => p.AreaSqm <= maxArea);
        if (!string.IsNullOrEmpty(plotNumber)) q = q.Where(p => p.PlotNumber.Contains(plotNumber));
        var rows = await q.OrderBy(p => p.Location!.Name).ThenBy(p => p.PlotNumber).Take(500).ToListAsync();
        return Ok(rows.Select(p => new { p.Id, p.LocationId, Location = p.Location!.Name, p.Location.District, p.PlotNumber,
            p.PlotSeries, p.AreaSqm, p.Category, p.RatePerSqm, Status = p.Status.ToString(), p.IsOfficiallyVerified,
            p.Remarks, p.LastUpdated, TotalValue = p.AreaSqm * p.RatePerSqm }));
    }

    [HttpPost("plots")]
    public async Task<IActionResult> AddPlot(Plot p)
    {
        if (await db.Plots.AnyAsync(x => x.LocationId == p.LocationId && x.PlotNumber == p.PlotNumber))
            return Conflict(new { message = "Plot number already exists for this MIDC." });
        p.Id = 0; p.Location = null; p.LastUpdated = DateTime.UtcNow; db.Plots.Add(p); await db.SaveChangesAsync(); return Ok(p);
    }

    [HttpPut("plots/{id:int}")]
    public async Task<IActionResult> UpdatePlot(int id, Plot p)
    {
        var e = await db.Plots.FindAsync(id); if (e is null) return NotFound();
        e.PlotNumber = p.PlotNumber; e.PlotSeries = p.PlotSeries; e.AreaSqm = p.AreaSqm; e.Category = p.Category;
        e.RatePerSqm = p.RatePerSqm; e.Status = p.Status; e.IsOfficiallyVerified = p.IsOfficiallyVerified;
        e.Remarks = p.Remarks; e.LayoutUrl = p.LayoutUrl; e.LastUpdated = DateTime.UtcNow;
        await db.SaveChangesAsync(); return NoContent();
    }
}
