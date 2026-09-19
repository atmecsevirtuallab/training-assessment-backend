using Microsoft.AspNetCore.Mvc;
using TrainingAndAssessmentWebAPI.Services;

namespace TrainingAndAssessmentWebAPI.Controllers;

[ApiController]
[Route("api/letterheads")]
public sealed class LetterheadController(WordReportService reports) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { configured = reports.HasTemplate });

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try { await reports.SaveTemplateAsync(file); return Ok(new { message = "Department letterhead uploaded successfully." }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("session-report")]
    public IActionResult Generate([FromBody] SessionWordReport request)
    {
        try { return File(reports.Generate(request), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{request.Batch}-{request.SessionName}-Report.docx"); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
