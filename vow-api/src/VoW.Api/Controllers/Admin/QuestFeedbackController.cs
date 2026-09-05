using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Feedback;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.ReportsView)]
[Route("admin/feedback/quests")]
public sealed class QuestFeedbackController(QuestFeedbackService service) : ControllerBase
{
    private bool ValidDates(QuestFeedbackQuery q) => q.To != DateOnly.MaxValue && (q.From is null || q.To is null || q.From <= q.To);

    [HttpGet]
    public async Task<IActionResult> Summary([FromQuery] QuestFeedbackQuery query, CancellationToken ct)
    {
        if (!ValidDates(query)) return BadRequest(new { message = "Invalid date range." });
        return Ok(await service.SummaryAsync(query, ct));
    }

    [HttpGet("detail")]
    public async Task<IActionResult> Detail([FromQuery, Required, StringLength(200, MinimumLength = 1)] string key,
        [FromQuery] QuestFeedbackQuery query, CancellationToken ct)
    {
        if (!ValidDates(query)) return BadRequest(new { message = "Invalid date range." });
        return Ok(await service.DetailAsync(key, query, ct));
    }
}
