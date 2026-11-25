using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Authorize, Route("api/customers/{customerId:int}/documents")]
public class DocumentsController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env) : ControllerBase
{
    static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };

    [HttpGet] public async Task<List<DocumentFile>> List(int customerId) =>
        await db.Documents.Where(d => d.CustomerId == customerId).OrderByDescending(d => d.UploadedAt).ToListAsync();

    [HttpPost, RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Upload(int customerId, [FromForm] string docType, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.Contains(ext)) return BadRequest(new { message = "File type not allowed." });
        if (!await db.Customers.AnyAsync(c => c.Id == customerId)) return NotFound();
        var dir = Path.Combine(env.ContentRootPath, cfg["Storage:DocumentsPath"]!, customerId.ToString());
        Directory.CreateDirectory(dir);
        var stored = $"{Guid.NewGuid():N}{ext}";                 // never trust the client's file name on disk
        await using (var fs = System.IO.File.Create(Path.Combine(dir, stored))) await file.CopyToAsync(fs);
        var d = new DocumentFile { CustomerId = customerId, DocType = docType, FileName = Path.GetFileName(file.FileName),
            StoredPath = Path.Combine(customerId.ToString(), stored), SizeBytes = file.Length };
        db.Documents.Add(d); await db.SaveChangesAsync(); return Ok(d);
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int customerId, int id)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);
        if (d is null) return NotFound();
        var path = Path.Combine(env.ContentRootPath, cfg["Storage:DocumentsPath"]!, d.StoredPath);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "application/octet-stream", d.FileName) : NotFound();
    }
}
