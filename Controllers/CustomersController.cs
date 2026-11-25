using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/customers")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    IQueryable<Customer> Filter(string? search, PipelineStage? stage, int? agentId, string? district)
    {
        var q = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.CompanyName.Contains(s) || c.ContactPerson.Contains(s) || c.Mobile.Contains(s) ||
                             c.CustomerCode.Contains(s) || (c.Agent != null && c.Agent.Name.Contains(s)));
        }
        if (stage.HasValue) q = q.Where(c => c.Stage == stage);
        if (agentId.HasValue) q = q.Where(c => c.AgentId == agentId);
        if (!string.IsNullOrEmpty(district)) q = q.Where(c => c.District == district);
        return q;
    }

    [HttpGet]
    public async Task<PagedResult<CustomerRow>> List(string? search, PipelineStage? stage, int? agentId, string? district,
        int page = 1, int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 5, 100);
        var q = Filter(search, stage, agentId, district);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CustomerRow(c.Id, c.CustomerCode, c.CompanyName, c.ContactPerson, c.Mobile,
                c.Agent!.Name, c.Stage, c.IsLost, c.NextFollowUpDate, c.LastContactedAt)).ToListAsync();
        return new(items, total, page, pageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Customer>> Get(int id) =>
        await db.Customers.AsNoTracking().Include(c => c.Agent).Include(c => c.Requirements)
            .FirstOrDefaultAsync(c => c.Id == id) is { } c ? c : NotFound();

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(Customer c)
    {
        if (await db.Customers.AnyAsync(x => x.Mobile == c.Mobile && x.CompanyName == c.CompanyName))
            return Conflict(new { message = "A customer with this company and mobile already exists." });
        c.Id = 0; c.Agent = null; c.Stage = PipelineStage.NewEnquiry; c.CreatedAt = DateTime.UtcNow;
        db.Customers.Add(c);
        db.AuditLogs.Add(new AuditLog { UserName = User.Identity?.Name, Action = "Create", Entity = "Customer", Details = c.CompanyName });
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Customer c)
    {
        var e = await db.Customers.FindAsync(id); if (e is null) return NotFound();
        db.Entry(e).CurrentValues.SetValues(new
        {
            c.CompanyName, c.ContactPerson, c.Designation, c.Mobile, c.AltMobile, c.Email, c.Address, c.City,
            c.District, c.State, c.IndustryType, c.GstNumber, c.PanNumber, c.Website, c.LeadSource, c.AgentId
        });
        db.AuditLogs.Add(new AuditLog { UserName = User.Identity?.Name, Action = "Update", Entity = "Customer", EntityId = id });
        await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPatch("{id:int}/stage")]
    public async Task<IActionResult> ChangeStage(int id, StageChange s)
    {
        var e = await db.Customers.FindAsync(id); if (e is null) return NotFound();
        e.Stage = s.Stage; e.IsLost = s.IsLost; e.LostReason = s.IsLost ? s.LostReason : null;
        await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPatch("{id:int}/assign/{agentId:int}")]
    public async Task<IActionResult> Assign(int id, int agentId)
    {
        var e = await db.Customers.FindAsync(id); if (e is null) return NotFound();
        if (!await db.Agents.AnyAsync(a => a.Id == agentId && a.IsActive)) return BadRequest(new { message = "Agent inactive or missing" });
        e.AgentId = agentId; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("{id:int}/requirements")]
    public async Task<IActionResult> AddRequirement(int id, CustomerRequirements r)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id)) return NotFound();
        r.Id = 0; r.CustomerId = id; db.Requirements.Add(r); await db.SaveChangesAsync(); return Ok(r);
    }

    [HttpGet("{id:int}/requirements/{reqId:int}/matches")]
    public async Task<IActionResult> Matches(int id, int reqId)
    {
        var r = await db.Requirements.FirstOrDefaultAsync(x => x.Id == reqId && x.CustomerId == id);
        if (r is null) return NotFound();
        var min = r.MinAreaSqm ?? r.RequiredAreaSqm * 0.75m;
        var max = r.MaxAreaSqm ?? r.RequiredAreaSqm * 1.25m;
        var rows = await db.Plots.Include(p => p.Location)
            .Where(p => (p.Status == PlotStatus.Vacant || p.Status == PlotStatus.AvailableForApplication)
                     && p.Category == r.PlotCategory && p.AreaSqm >= min
                     && (r.PreferredLocationId == null || p.LocationId == r.PreferredLocationId)
                     && (r.RequiredDistrict == null || p.Location!.District == r.RequiredDistrict))
            .OrderBy(p => Math.Abs(p.AreaSqm - r.RequiredAreaSqm)).Take(30).ToListAsync();
        return Ok(rows.Select(p => new {
            p.Id, Location = p.Location!.Name, p.Location.District, p.PlotNumber, p.Category,
            AvailableArea = p.AreaSqm, r.RequiredAreaSqm, AreaDifference = p.AreaSqm - r.RequiredAreaSqm,
            p.RatePerSqm, EstimatedCost = p.AreaSqm * p.RatePerSqm, Status = p.Status.ToString(), p.IsOfficiallyVerified,
            MatchStatus = r.Budget != null && p.AreaSqm * p.RatePerSqm > r.Budget ? "Over budget"
                        : p.AreaSqm <= max ? "Good match" : "Partial match" }));
    }

    [HttpGet("export")]
    public async Task<FileResult> Export(string? search, PipelineStage? stage, int? agentId, string? district)
    {
        var rows = await Filter(search, stage, agentId, district).Include(c => c.Agent).OrderBy(c => c.Id).ToListAsync();
        using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Customers");
        string[] h = { "Code", "Company", "Contact", "Mobile", "Email", "District", "Agent", "Stage", "Next follow-up" };
        for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
        for (int r = 0; r < rows.Count; r++)
        {
            var c = rows[r]; int n = r + 2;
            ws.Cell(n, 1).SetValue(c.CustomerCode); ws.Cell(n, 2).SetValue(c.CompanyName); ws.Cell(n, 3).SetValue(c.ContactPerson);
            ws.Cell(n, 4).SetValue(c.Mobile); ws.Cell(n, 5).SetValue(c.Email ?? ""); ws.Cell(n, 6).SetValue(c.District ?? "");
            ws.Cell(n, 7).SetValue(c.Agent?.Name ?? ""); ws.Cell(n, 8).SetValue(c.Stage.ToString());
            ws.Cell(n, 9).SetValue(c.NextFollowUpDate?.ToString("dd-MMM-yyyy") ?? "");
        }
        ws.Columns().AdjustToContents();
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        return File(ms, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"customers-{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
