using Microsoft.AspNetCore.Mvc;
using VoW.Api.Controllers;
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
            ModelState.AddErrors(result.Errors);
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
            ModelState.AddErrors(result.Errors);
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
            ModelState.AddErrors(result.Errors);
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

    [HttpPut("npcs/{npcId:int}/image")]
    [RequestSizeLimit(NpcImageMaxSizeBytes)]
    public async Task<IActionResult> UploadNpcImage(
        int npcId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "An image file is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > NpcImageMaxSizeBytes)
        {
            ModelState.AddModelError(nameof(file), $"Image file must not exceed {NpcImageMaxSizeBytes} bytes.");
            return ValidationProblem(ModelState);
        }

        if (!IsAcceptedImage(file))
        {
            ModelState.AddModelError(nameof(file), "Only PNG, JPEG, or WebP images are accepted.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await contentService.UploadNpcImageAsync(npcId, stream, cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPut("quests/{questId:int}/npcs/{npcId:int}/recordings")]
    [RequestSizeLimit(NpcRecordingsMaxSizeBytes)]
    public async Task<IActionResult> UploadNpcRecordings(
        int questId,
        int npcId,
        [FromForm] List<IFormFile>? recordings,
        [FromForm] bool overwrite,
        CancellationToken cancellationToken)
    {
        if (recordings is null || recordings.Count == 0)
        {
            ModelState.AddModelError(nameof(recordings), "At least one recording file is required.");
            return ValidationProblem(ModelState);
        }

        var uploads = recordings
            .Select(file => new NpcRecordingUpload(
                file.FileName,
                file.ContentType,
                file.Length,
                file.OpenReadStream))
            .ToArray();

        var result = await contentService.UploadNpcRecordingsAsync(
            questId,
            npcId,
            uploads,
            overwrite,
            cancellationToken);

        if (!result.Found)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            return Ok(result.Response);
        }

        ModelState.AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }

    [HttpPut("recordings/mass")]
    [RequestSizeLimit(NpcRecordingsMaxSizeBytes)]
    public async Task<IActionResult> UploadMassNpcRecordings(
        [FromForm] List<IFormFile>? recordings,
        [FromForm] bool overwrite,
        [FromForm] int? questId,
        [FromForm] int? npcId,
        CancellationToken cancellationToken)
    {
        if (recordings is null || recordings.Count == 0)
        {
            ModelState.AddModelError(nameof(recordings), "At least one recording file is required.");
            return ValidationProblem(ModelState);
        }

        var uploads = recordings
            .Select(file => new NpcRecordingUpload(
                file.FileName,
                file.ContentType,
                file.Length,
                file.OpenReadStream))
            .ToArray();

        var result = await contentService.UploadMassNpcRecordingsAsync(
            uploads,
            overwrite,
            questId,
            npcId,
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Response);
        }

        ModelState.AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }

    [HttpGet("quests/{questId:int}/npcs/{npcId:int}/recordings")]
    public async Task<ActionResult<IReadOnlyCollection<NpcRecordingResponse>>> GetNpcRecordings(
        int questId,
        int npcId,
        CancellationToken cancellationToken)
    {
        var result = await contentService.GetNpcRecordingsAsync(questId, npcId, cancellationToken);
        return result.Found ? Ok(result.Recordings) : NotFound();
    }

    [HttpDelete("quests/{questId:int}/npcs/{npcId:int}/recordings/{recordingId:int}")]
    public async Task<IActionResult> DeleteNpcRecording(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken)
    {
        var result = await contentService.DeleteNpcRecordingAsync(
            questId,
            npcId,
            recordingId,
            cancellationToken);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    private const int QuestScriptMaxSizeBytes = 2_000_000;
    private const int NpcImageMaxSizeBytes = 8_000_000;
    private const int NpcRecordingsMaxSizeBytes = 500_000_000;

    private static readonly string[] AcceptedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private static readonly string[] AcceptedImageContentTypes = ["image/png", "image/jpeg", "image/webp"];

    private static bool IsAcceptedImage(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrEmpty(extension) &&
            AcceptedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(file.ContentType) &&
            AcceptedImageContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase);
    }

    private IActionResult ProblemFrom(ContentMutationResult result)
    {
        if (!result.Found)
        {
            return NotFound();
        }

        ModelState.AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }
}
