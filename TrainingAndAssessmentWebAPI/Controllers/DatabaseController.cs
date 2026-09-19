using Microsoft.AspNetCore.Mvc;
using TrainingAndAssessmentWebAPI.Services;

namespace TrainingAndAssessmentWebAPI.Controllers;

[ApiController]
[Route("api/database")]
public sealed class DatabaseController(DatabaseBackupService backupService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = await backupService.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("backup")]
    public async Task<IActionResult> Backup()
    {
        try
        {
            var result = await backupService.BackupAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("update")]
    [HttpPost("restore")]
    public async Task<IActionResult> Restore()
    {
        try
        {
            var result = await backupService.RestoreAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
