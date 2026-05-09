using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Content;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Content;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.ContentManage)]
[Route("admin/content")]
public sealed class ContentController(IContentService contentService) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<ContentOptionsResponse>> Options(CancellationToken cancellationToken) =>
        Ok(await contentService.GetOptionsAsync(cancellationToken));

    [HttpPost("quests")]
    public async Task<ActionResult<CreateContentResponse>> CreateQuest(
        [FromBody] CreateQuestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.CreateQuestAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var (field, message) in result.Errors)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new CreateContentResponse(result.Id!.Value));
    }

    [HttpPost("npcs")]
    public async Task<ActionResult<CreateContentResponse>> CreateNpc(
        [FromBody] CreateNpcRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.CreateNpcAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var (field, message) in result.Errors)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new CreateContentResponse(result.Id!.Value));
    }
}
