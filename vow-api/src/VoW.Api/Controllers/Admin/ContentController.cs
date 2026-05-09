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

    [HttpGet("search")]
    public async Task<ActionResult<ContentSearchResponse>> Search(
        [FromQuery] ContentSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.SearchAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    [HttpPost("quests")]
    public async Task<ActionResult<CreateContentResponse>> CreateQuest(
        [FromBody] CreateQuestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.CreateQuestAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
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
            AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(new CreateContentResponse(result.Id!.Value));
    }

    [HttpPatch("quests/{questId:int}")]
    public async Task<IActionResult> UpdateQuest(
        int questId,
        [FromBody] UpdateContentNameRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateQuestAsync(questId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpDelete("quests/{questId:int}")]
    public async Task<IActionResult> DeleteQuest(int questId, CancellationToken cancellationToken)
    {
        var result = await contentService.DeleteQuestAsync(questId, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPatch("quests/{questId:int}/writer")]
    public async Task<IActionResult> UpdateQuestWriter(
        int questId,
        [FromBody] UpdateQuestWriterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateQuestWriterAsync(questId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPatch("npcs/{npcId:int}")]
    public async Task<IActionResult> UpdateNpc(
        int npcId,
        [FromBody] UpdateContentNameRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateNpcAsync(npcId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPatch("npcs/{npcId:int}/voice-actor")]
    public async Task<IActionResult> UpdateNpcVoiceActor(
        int npcId,
        [FromBody] UpdateNpcVoiceActorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateNpcVoiceActorAsync(npcId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPost("quests/{questId:int}/npcs")]
    public async Task<IActionResult> LinkNpcToQuest(
        int questId,
        [FromBody] LinkQuestNpcRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.LinkNpcToQuestAsync(questId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPatch("quests/{questId:int}/npcs/{npcId:int}/sound-editor")]
    public async Task<IActionResult> UpdateQuestNpcSoundEditor(
        int questId,
        int npcId,
        [FromBody] UpdateQuestNpcSoundEditorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateQuestNpcSoundEditorAsync(questId, npcId, request, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpDelete("quests/{questId:int}/npcs/{npcId:int}")]
    public async Task<IActionResult> UnlinkNpcFromQuest(
        int questId,
        int npcId,
        CancellationToken cancellationToken)
    {
        var result = await contentService.UnlinkNpcFromQuestAsync(questId, npcId, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPut("quests/{questId:int}/script")]
    [RequestSizeLimit(QuestScriptMaxSizeBytes)]
    public async Task<IActionResult> UploadQuestScript(
        int questId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "A script file is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > QuestScriptMaxSizeBytes)
        {
            ModelState.AddModelError(nameof(file), $"Script file must not exceed {QuestScriptMaxSizeBytes} bytes.");
            return ValidationProblem(ModelState);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(file), "Only .txt files are accepted.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await contentService.UploadQuestScriptAsync(questId, stream, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    private const int QuestScriptMaxSizeBytes = 2_000_000;

    private IActionResult ProblemFrom(ContentMutationResult result)
    {
        if (!result.Found)
        {
            return NotFound();
        }

        AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }

    private void AddErrors(IReadOnlyDictionary<string, string> errors)
    {
        foreach (var (field, message) in errors)
        {
            ModelState.AddModelError(field, message);
        }
    }
}
