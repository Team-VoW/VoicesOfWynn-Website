using System.Text.RegularExpressions;
using System.Globalization;
using MySqlConnector;
using VoW.Api.Contracts.Content;
using VoW.Api.Domain.Content;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Content;

public sealed partial class ContentService(
    IContentRepository contentRepository,
    IQuestScriptStorage questScriptStorage,
    INpcImageStorage npcImageStorage) : IContentService
{
    private const int ContentNameMaxLength = 63;
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];

    public async Task<ContentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var quests = await contentRepository.GetQuestsAsync(cancellationToken);
        var npcs = await contentRepository.GetNpcsAsync(cancellationToken);
        var writers = await GetUsersByRoleAsync(ContentUserRole.Writer, cancellationToken);
        var voiceActors = await GetUsersByRoleAsync(ContentUserRole.VoiceActor, cancellationToken);
        var soundEditors = await GetUsersByRoleAsync(ContentUserRole.SoundEditor, cancellationToken);

        return new ContentOptionsResponse(
            ToResponse(quests),
            ToResponse(npcs),
            ToResponse(writers),
            ToResponse(voiceActors),
            ToResponse(soundEditors));
    }

    public async Task<ContentSearchServiceResult> SearchAsync(
        ContentSearchRequest request,
        CancellationToken cancellationToken)
    {
        var pageSize = AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : 25;
        var page = Math.Max(1, request.Page);
        var criteria = new ContentSearchCriteria(
            NormalizeFilter(request.Quest),
            NormalizeFilter(request.Npc),
            page,
            pageSize);

        var resultPage = await contentRepository.SearchAsync(criteria, cancellationToken);

        var scriptUrlTasks = resultPage.Results
            .Select(quest => ResolveScriptUrlAsync(quest.QuestDegeneratedName, cancellationToken))
            .ToArray();
        var scriptUrls = await Task.WhenAll(scriptUrlTasks);

        var results = resultPage.Results
            .Select((quest, index) => new QuestContentResult(
                quest.QuestId,
                quest.QuestName,
                quest.QuestDegeneratedName,
                quest.WriterId,
                quest.WriterName,
                scriptUrls[index],
                quest.Npcs.Select(npc => new NpcContentResult(
                    npc.NpcId,
                    npc.NpcName,
                    npc.NpcDegeneratedName,
                    npc.VoiceActorId,
                    npc.VoiceActorName,
                    npc.SoundEditorId,
                    npc.SoundEditorName,
                    npc.RecordingCount)).ToArray()))
            .ToArray();

        return ContentSearchServiceResult.Success(new ContentSearchResponse(
            resultPage.Total,
            resultPage.Page,
            resultPage.PageSize,
            results));
    }

    private async Task<string?> ResolveScriptUrlAsync(string degeneratedName, CancellationToken cancellationToken) =>
        await questScriptStorage.ScriptExistsAsync(degeneratedName, cancellationToken)
            ? questScriptStorage.GetScriptUrl(degeneratedName).ToString()
            : null;

    public async Task<ContentMutationResult> CreateQuestAsync(
        CreateQuestRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var nameError = ValidateName(name, "Quest name");
        if (nameError is not null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), nameError);
        }

        var degeneratedName = DegenerateName(name!);
        if (degeneratedName is null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "Quest name must contain at least one alphanumeric character.");
        }

        if (await contentRepository.QuestDegeneratedNameExistsAsync(degeneratedName, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "A quest with this name or a similar degenerated name already exists.");
        }

        if (request.WriterUserId is not null &&
            !await OptionExistsAsync(ContentUserRole.Writer, request.WriterUserId.Value, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.WriterUserId), "Writer must be an existing writer user.");
        }

        try
        {
            var created = await contentRepository.CreateQuestAsync(
                new CreateQuestCommand(name!, request.WriterUserId),
                degeneratedName,
                cancellationToken);
            return ContentMutationResult.Success(created.Id);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "A quest with this name or a similar degenerated name already exists.");
        }
    }

    public async Task<ContentMutationResult> CreateNpcAsync(
        CreateNpcRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var nameError = ValidateName(name, "NPC name");
        if (nameError is not null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), nameError);
        }

        var degeneratedName = DegenerateName(name!);
        if (degeneratedName is null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "NPC name must contain at least one alphanumeric character.");
        }

        var assignments = request.QuestAssignments?.ToArray() ?? [];
        if (assignments.Length == 0)
        {
            return ContentMutationResult.Invalid(nameof(request.QuestAssignments), "At least one quest must be selected.");
        }

        if (assignments.Select(a => a.QuestId).Distinct().Count() != assignments.Length)
        {
            return ContentMutationResult.Invalid(nameof(request.QuestAssignments), "Each quest can only be selected once.");
        }

        var quests = await contentRepository.GetQuestsAsync(cancellationToken);
        var questIds = quests.Select(q => q.Id).ToHashSet();
        if (assignments.Any(a => !questIds.Contains(a.QuestId)))
        {
            return ContentMutationResult.Invalid(nameof(request.QuestAssignments), "Every selected quest must exist.");
        }

        if (request.VoiceActorUserId is not null &&
            !await OptionExistsAsync(ContentUserRole.VoiceActor, request.VoiceActorUserId.Value, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.VoiceActorUserId), "Voice actor must be an existing actor user.");
        }

        var soundEditorIds = assignments
            .Select(a => a.SoundEditorUserId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (soundEditorIds.Length > 0)
        {
            var soundEditors = await GetUsersByRoleAsync(ContentUserRole.SoundEditor, cancellationToken);
            var validSoundEditorIds = soundEditors.Select(u => u.Id).ToHashSet();
            if (soundEditorIds.Any(id => !validSoundEditorIds.Contains(id)))
            {
                return ContentMutationResult.Invalid(nameof(request.QuestAssignments), "Sound editors must be existing sound editor users.");
            }
        }

        var command = new CreateNpcCommand(
            name!,
            request.VoiceActorUserId,
            assignments.Select(a => new CreateNpcQuestAssignment(a.QuestId, a.SoundEditorUserId)).ToArray());
        var created = await contentRepository.CreateNpcAsync(command, degeneratedName, cancellationToken);
        return ContentMutationResult.Success(created.Id);
    }

    public async Task<ContentMutationResult> UpdateQuestAsync(
        int questId,
        UpdateContentNameRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var nameError = ValidateName(name, "Quest name");
        if (nameError is not null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), nameError);
        }

        var degeneratedName = DegenerateName(name!);
        if (degeneratedName is null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "Quest name must contain at least one alphanumeric character.");
        }

        if (!await contentRepository.QuestExistsAsync(questId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (await contentRepository.QuestDegeneratedNameExistsAsync(questId, degeneratedName, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "A quest with this name or a similar degenerated name already exists.");
        }

        try
        {
            return await contentRepository.UpdateQuestAsync(questId, name!, degeneratedName, cancellationToken)
                ? ContentMutationResult.Success()
                : ContentMutationResult.NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "A quest with this name or a similar degenerated name already exists.");
        }
    }

    public async Task<ContentMutationResult> DeleteQuestAsync(int questId, CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestExistsAsync(questId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (await contentRepository.QuestHasNpcsAsync(questId, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(questId), "Quest cannot be deleted while NPCs are linked to it.");
        }

        return await contentRepository.DeleteQuestAsync(questId, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UpdateQuestWriterAsync(
        int questId,
        UpdateQuestWriterRequest request,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestExistsAsync(questId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (request.WriterUserId is not null &&
            !await OptionExistsAsync(ContentUserRole.Writer, request.WriterUserId.Value, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.WriterUserId), "Writer must be an existing writer user.");
        }

        return await contentRepository.UpdateQuestWriterAsync(questId, request.WriterUserId, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UpdateNpcAsync(
        int npcId,
        UpdateContentNameRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var nameError = ValidateName(name, "NPC name");
        if (nameError is not null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), nameError);
        }

        var degeneratedName = DegenerateName(name!);
        if (degeneratedName is null)
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "NPC name must contain at least one alphanumeric character.");
        }

        if (!await contentRepository.NpcExistsAsync(npcId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (await contentRepository.NpcDegeneratedNameConflictsForLinkedQuestsAsync(npcId, degeneratedName, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.Name), "An NPC with this name or a similar degenerated name is already linked to one of this NPC's quests.");
        }

        return await contentRepository.UpdateNpcAsync(npcId, name!, degeneratedName, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UpdateNpcVoiceActorAsync(
        int npcId,
        UpdateNpcVoiceActorRequest request,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.NpcExistsAsync(npcId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (request.VoiceActorUserId is not null &&
            !await OptionExistsAsync(ContentUserRole.VoiceActor, request.VoiceActorUserId.Value, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.VoiceActorUserId), "Voice actor must be an existing actor user.");
        }

        return await contentRepository.UpdateNpcVoiceActorAsync(npcId, request.VoiceActorUserId, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> LinkNpcToQuestAsync(
        int questId,
        LinkQuestNpcRequest request,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestExistsAsync(questId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        var degeneratedName = await contentRepository.GetNpcDegeneratedNameAsync(request.NpcId, cancellationToken);
        if (degeneratedName is null)
        {
            return ContentMutationResult.Invalid(nameof(request.NpcId), "NPC must exist.");
        }

        if (await contentRepository.QuestNpcLinkExistsAsync(questId, request.NpcId, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.NpcId), "NPC is already linked to this quest.");
        }

        if (await contentRepository.NpcDegeneratedNameConflictsInQuestAsync(questId, request.NpcId, degeneratedName, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.NpcId), "An NPC with this name or a similar degenerated name is already linked to this quest.");
        }

        return await contentRepository.LinkNpcToQuestAsync(questId, request.NpcId, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UpdateQuestNpcSoundEditorAsync(
        int questId,
        int npcId,
        UpdateQuestNpcSoundEditorRequest request,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestNpcLinkExistsAsync(questId, npcId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (request.SoundEditorUserId is not null &&
            !await OptionExistsAsync(ContentUserRole.SoundEditor, request.SoundEditorUserId.Value, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(request.SoundEditorUserId), "Sound editor must be an existing sound editor user.");
        }

        return await contentRepository.UpdateQuestNpcSoundEditorAsync(
                questId,
                npcId,
                request.SoundEditorUserId,
                cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UnlinkNpcFromQuestAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestNpcLinkExistsAsync(questId, npcId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        if (await contentRepository.QuestNpcHasRecordingsAsync(questId, npcId, cancellationToken))
        {
            return ContentMutationResult.Invalid(nameof(npcId), "NPC cannot be unlinked from this quest while recordings exist.");
        }

        return await contentRepository.UnlinkNpcFromQuestAsync(questId, npcId, cancellationToken)
            ? ContentMutationResult.Success()
            : ContentMutationResult.NotFound();
    }

    public async Task<ContentMutationResult> UploadQuestScriptAsync(
        int questId,
        Stream content,
        CancellationToken cancellationToken)
    {
        var degeneratedName = await contentRepository.GetQuestDegeneratedNameAsync(questId, cancellationToken);
        if (degeneratedName is null)
        {
            return ContentMutationResult.NotFound();
        }

        await questScriptStorage.UploadScriptAsync(degeneratedName, content, cancellationToken);
        return ContentMutationResult.Success();
    }

    public async Task<ContentMutationResult> UploadNpcImageAsync(
        int npcId,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.NpcExistsAsync(npcId, cancellationToken))
        {
            return ContentMutationResult.NotFound();
        }

        MemoryStream normalized;
        try
        {
            normalized = await NpcImagePipeline.NormalizeToWebpAsync(content, cancellationToken);
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            return ContentMutationResult.Invalid("file", "The uploaded file is not a recognized image.");
        }
        catch (SixLabors.ImageSharp.InvalidImageContentException)
        {
            return ContentMutationResult.Invalid("file", "The uploaded image is corrupted or could not be decoded.");
        }

        await using (normalized)
        {
            await npcImageStorage.UploadImageAsync(npcId, normalized, cancellationToken);
        }
        return ContentMutationResult.Success();
    }

    private async Task<bool> OptionExistsAsync(
        ContentUserRole role,
        int id,
        CancellationToken cancellationToken)
    {
        var users = await GetUsersByRoleAsync(role, cancellationToken);
        return users.Any(user => user.Id == id);
    }

    private Task<IReadOnlyCollection<ContentOption>> GetUsersByRoleAsync(
        ContentUserRole role,
        CancellationToken cancellationToken) =>
        contentRepository.GetUsersByRolesAsync(ContentRolePolicy.RolesFor(role), cancellationToken);

    private static IReadOnlyCollection<ContentOptionResponse> ToResponse(IEnumerable<ContentOption> options) =>
        options.Select(option => new ContentOptionResponse(option.Id, option.Name)).ToArray();

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ValidateName(string? name, string label)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"{label} cannot be empty.";
        }

        return new StringInfo(name).LengthInTextElements > ContentNameMaxLength
            ? $"{label} cannot be longer than {ContentNameMaxLength} characters."
            : null;
    }

    private static string? DegenerateName(string name)
    {
        var degenerated = DegeneratedNameRegex().Replace(name.ToLowerInvariant(), string.Empty);
        return degenerated.Length == 0 ? null : degenerated;
    }

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex DegeneratedNameRegex();
}
