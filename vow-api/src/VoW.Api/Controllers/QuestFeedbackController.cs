using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Services.Feedback;

namespace VoW.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("feedback/quests")]
[RequestSizeLimit(16384)]
public sealed class QuestFeedbackController(QuestFeedbackService service) : ControllerBase
{
    private string ConnectionIp => HttpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";

    [HttpPost]
    public async Task<IActionResult> Rate(QuestRatingRequest request, CancellationToken ct) =>
        Result(await service.RateAsync(request, ConnectionIp, ct));

    [HttpPut("{id:guid}/comment")]
    public async Task<IActionResult> Comment(Guid id, QuestCommentRequest request, CancellationToken ct) =>
        Result(await service.CommentAsync(id, request, ConnectionIp, ct));

    private IActionResult Result(FeedbackWriteResult result)
    {
        Response.Headers.CacheControl = "no-store";
        if (result.Status == 503)
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(503, new { message = "Quest feedback is temporarily unavailable. Please try again later." });
        }
        if (result.Status == 429)
        {
            var now = DateTime.UtcNow;
            var seconds = 3600 - (now.Minute * 60 + now.Second);
            Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(429, new { message = "Too many feedback requests. Retry after the indicated delay.", retryAfterSeconds = seconds });
        }
        return result.Rating is not null ? Ok(result.Rating) : StatusCode(result.Status);
    }
}
