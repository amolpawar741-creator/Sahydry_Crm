using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/quotations")]
public class QuotationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int? customerId)
    {
        var rows = await db.Quotations.AsNoTracking().Include(q => q.Customer).Include(q => q.Payments)
            .Where(q => customerId == null || q.CustomerId == customerId).OrderByDescending(q => q.Id).Take(300).ToListAsync();
        return Ok(rows.Select(q => {
            var paid = q.Payments.Sum(p => p.Amount) + 0m;
            return new { q.Id, q.QuotationNo, q.QuotationDate, Customer = q.Customer!.CompanyName, q.AreaSqm, q.PlotValue,
                q.ConsultancyFee, q.GstAmount, q.GrandTotal, q.AdvanceAmount, Paid = paid,
                Outstanding = q.GrandTotal - q.AdvanceAmount - paid,
                PaymentStatus = q.GrandTotal - q.AdvanceAmount - paid <= 0 ? "Paid" : paid + q.AdvanceAmount > 0 ? "Partial" : "Unpaid" };
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(QuotationCreate d)
    {
        if (d.AreaSqm <= 0 || d.ConsultancyRate < 0 || d.MidcRate < 0) return BadRequest(new { message = "Area and rates must be positive." });
        var grand = d.AreaSqm * d.ConsultancyRate * (1 + d.GstPercent / 100);
        if (d.AdvanceAmount > grand) return BadRequest(new { message = "Advance cannot exceed the total payable." });
        if (!await db.Customers.AnyAsync(c => c.Id == d.CustomerId)) return NotFound();
        var q = new Quotation { CustomerId = d.CustomerId, PlotId = d.PlotId, AreaSqm = d.AreaSqm, MidcRate = d.MidcRate,
            ConsultancyRate = d.ConsultancyRate, GstPercent = d.GstPercent, AdvanceAmount = d.AdvanceAmount,
            PaymentTerms = d.PaymentTerms, ValidUntil = d.ValidUntil, Remarks = d.Remarks };
        db.Quotations.Add(q);
        var cust = await db.Customers.FindAsync(d.CustomerId);
        if (cust!.Stage < PipelineStage.QuotationSubmitted) cust.Stage = PipelineStage.QuotationSubmitted;
        await db.SaveChangesAsync();
        await db.Entry(q).ReloadAsync();                       // read DB-computed columns
        return Ok(q);
    }

    [HttpPost("{id:int}/payments")]
    public async Task<IActionResult> AddPayment(int id, PaymentCreate d)
    {
        var q = await db.Quotations.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return NotFound();
        var outstanding = q.GrandTotal - q.AdvanceAmount - q.Payments.Sum(p => p.Amount);
        if (d.Amount <= 0 || d.Amount > outstanding) return BadRequest(new { message = $"Amount must be between 0 and {outstanding:N2}." });
        db.Payments.Add(new Payment { QuotationId = id, PaymentDate = d.PaymentDate, Amount = d.Amount, Mode = d.Mode, Reference = d.Reference });
        await db.SaveChangesAsync(); return Ok();
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var q = await db.Quotations.AsNoTracking().Include(x => x.Customer).Include(x => x.Plot).ThenInclude(p => p!.Location)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return NotFound();
        string inr(decimal v) => "₹ " + v.ToString("N2", new System.Globalization.CultureInfo("en-IN"));

        var bytes = Document.Create(doc => doc.Page(page =>
        {
            page.Margin(40); page.DefaultTextStyle(t => t.FontSize(11));
            page.Header().Column(h =>
            {
                h.Item().Text("SHAYDRE WORKFORCE PVT. LTD.").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                h.Item().Text("MIDC Consultancy Quotation").FontSize(12).FontColor(Colors.Grey.Darken2);
            });
            page.Content().PaddingVertical(15).Column(c =>
            {
                c.Spacing(6);
                c.Item().Text($"Quotation No: {q.QuotationNo}      Date: {q.QuotationDate:dd-MMM-yyyy}");
                c.Item().Text($"To: {q.Customer!.CompanyName} ({q.Customer.ContactPerson})");
                c.Item().Text($"MIDC: {q.Plot?.Location?.Name ?? "-"}    Plot: {q.Plot?.PlotNumber ?? "-"}");
                c.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); });
                    void Row(string a, string b, bool bold = false)
                    {
                        //t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(a).Bold(bold);
                        //t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).AlignRight().Text(b).Bold(bold);
                    }
                    Row("Plot area (sq. m)", q.AreaSqm.ToString("N2"));
                    Row("MIDC rate per sq. m", inr(q.MidcRate));
                    Row("Total MIDC plot value", inr(q.PlotValue));
                    Row("Consultancy charge per sq. m", inr(q.ConsultancyRate));
                    Row("Total consultancy fee", inr(q.ConsultancyFee));
                    Row($"GST @ {q.GstPercent}%", inr(q.GstAmount));
                    Row("Total payable", inr(q.GrandTotal), true);
                    Row("Advance", inr(q.AdvanceAmount));
                    Row("Balance payable", inr(q.GrandTotal - q.AdvanceAmount), true);
                });
                c.Item().PaddingTop(10).Text($"Payment terms: {q.PaymentTerms ?? "-"}");
                c.Item().Text($"Valid until: {q.ValidUntil:dd-MMM-yyyy}");
                if (!string.IsNullOrWhiteSpace(q.Remarks)) c.Item().Text($"Remarks: {q.Remarks}");
                c.Item().PaddingTop(8).Text("MIDC plot value is indicative and payable to MIDC; it is not part of the consultancy fee.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Page "); t.CurrentPageNumber(); });
        })).GeneratePdf();
        return File(bytes, "application/pdf", $"{q.QuotationNo}.pdf");
    }
}
