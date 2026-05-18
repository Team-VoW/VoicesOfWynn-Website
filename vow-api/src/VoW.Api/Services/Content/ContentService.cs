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
    INpcImageStorage npcImageStorage,
    INpcRecordingStorage npcRecordingStorage) : IContentService
{
    private const int ContentNameMaxLength = 63;
    private const int RecordingFileNameMaxLength = 63;
    private const short RecordingLineMinValue = 1;
    private const short RecordingLineMaxValue = short.MaxValue;
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];
    private static readonly string[] AcceptedRecordingContentTypes = ["audio/ogg", "video/ogg", "application/ogg"];

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

    public async Task<ContentMutationResult> ArchiveNpcAsync(
        int npcId,
        ArchiveNpcRequest request,
        CancellationToken cancellationToken)
    {
        var archiveData = await contentRepository.GetNpcArchiveDataAsync(npcId, cancellationToken);
        if (archiveData is null)
        {
            return ContentMutationResult.NotFound();
        }

        if (archiveData.Archived)
        {
            return ContentMutationResult.Invalid(nameof(npcId), "NPC is already archived.");
        }

        var archiveDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var archivedRecordings = new List<ArchivedRecordingFile>(archiveData.Recordings.Count);
        var deletedRecordingIds = new List<int>();
        foreach (var recording in archiveData.Recordings)
        {
            var archivedFileName = CreateArchivedRecordingFileName(recording.File, recording.RecordingId, archiveDate);
            var renamed = await npcRecordingStorage.TryRenameRecordingAsync(
                recording.File,
                archivedFileName,
                cancellationToken);
            if (renamed)
            {
                archivedRecordings.Add(new ArchivedRecordingFile(recording.RecordingId, archivedFileName));
            }
            else
            {
                deletedRecordingIds.Add(recording.RecordingId);
            }
        }

        var replacementNpcId = await contentRepository.ArchiveNpcAsync(
            npcId,
            request.CreateReplacement,
            archivedRecordings,
            deletedRecordingIds,
            cancellationToken);
        if (replacementNpcId is null && request.CreateReplacement)
        {
            return ContentMutationResult.NotFound();
        }

        if (replacementNpcId is not null)
        {
            await npcImageStorage.CopyImageIfExistsAsync(npcId, replacementNpcId.Value, cancellationToken);
        }

        return replacementNpcId is null
            ? ContentMutationResult.Success()
            : ContentMutationResult.Success(replacementNpcId.Value);
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

    public async Task<NpcRecordingUploadServiceResult> UploadNpcRecordingsAsync(
        int questId,
        int npcId,
        IReadOnlyCollection<NpcRecordingUpload> recordings,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestNpcLinkExistsAsync(questId, npcId, cancellationToken))
        {
            return NpcRecordingUploadServiceResult.NotFound();
        }

        var results = new List<UploadNpcRecordingResult>(recordings.Count);
        foreach (var recording in recordings)
        {
            results.Add(await UploadNpcRecordingAsync(questId, npcId, recording, overwrite, cancellationToken));
        }

        return NpcRecordingUploadServiceResult.Success(new UploadNpcRecordingsResponse(results));
    }

    public async Task<NpcRecordingUploadServiceResult> UploadMassNpcRecordingsAsync(
        IReadOnlyCollection<NpcRecordingUpload> recordings,
        bool overwrite,
        int? questId,
        int? npcId,
        CancellationToken cancellationToken)
    {
        var results = new List<UploadNpcRecordingResult>(recordings.Count);
        foreach (var recording in recordings)
        {
            results.Add(await UploadMassNpcRecordingAsync(recording, overwrite, questId, npcId, cancellationToken));
        }

        return NpcRecordingUploadServiceResult.Success(new UploadNpcRecordingsResponse(results));
    }

    public async Task<NpcRecordingsServiceResult> GetNpcRecordingsAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken)
    {
        if (!await contentRepository.QuestNpcLinkExistsAsync(questId, npcId, cancellationToken))
        {
            return NpcRecordingsServiceResult.NotFound();
        }

        var recordings = await contentRepository.GetQuestNpcRecordingsAsync(questId, npcId, cancellationToken);
        var response = recordings
            .Select(recording => new NpcRecordingResponse(
                recording.RecordingId,
                recording.Line,
                recording.FileName,
                npcRecordingStorage.GetRecordingUrl(recording.FileName).ToString()))
            .ToArray();

        return NpcRecordingsServiceResult.Success(response);
    }

    public async Task<ContentMutationResult> DeleteNpcRecordingAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken)
    {
        var recording = await contentRepository.GetQuestNpcRecordingFileAsync(
            questId,
            npcId,
            recordingId,
            cancellationToken);
        if (recording is null)
        {
            return ContentMutationResult.NotFound();
        }

        var deleted = await contentRepository.DeleteQuestNpcRecordingAsync(
            questId,
            npcId,
            recordingId,
            cancellationToken);
        if (!deleted)
        {
            return ContentMutationResult.NotFound();
        }

        await npcRecordingStorage.DeleteRecordingAsync(recording.File, cancellationToken);
        return ContentMutationResult.Success();
    }

    private async Task<UploadNpcRecordingResult> UploadNpcRecordingAsync(
        int questId,
        int npcId,
        NpcRecordingUpload recording,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(recording.FileName);
        var validation = ValidateRecordingFile(fileName, recording.ContentType, recording.Length);
        if (validation is not null)
        {
            return validation;
        }

        var line = ParseRecordingLine(fileName);
        if (await contentRepository.RecordingFileBelongsToDifferentRecordingAsync(
                fileName,
                questId,
                npcId,
                line,
                cancellationToken))
        {
            return RecordingResult(
                fileName,
                409,
                "Conflict",
                "A recording with this filename is already connected to a different quest, NPC, or line.");
        }

        var conflictExists = await npcRecordingStorage.RecordingExistsAsync(fileName, cancellationToken);
        if (conflictExists && !overwrite)
        {
            var renamedFileName = CreateConflictRecordingFileName(fileName);

            await npcRecordingStorage.RenameRecordingAsync(fileName, renamedFileName, cancellationToken);

            var existingRecording = await contentRepository.GetRecordingByFileAsync(fileName, cancellationToken);
            if (existingRecording is not null)
            {
                await contentRepository.UpdateRecordingFileAsync(
                    existingRecording.RecordingId,
                    renamedFileName,
                    cancellationToken);
            }

            await using var content = recording.OpenReadStream();
            await npcRecordingStorage.UploadRecordingAsync(fileName, content, cancellationToken);
            await contentRepository.InsertRecordingAsync(questId, npcId, line, fileName, cancellationToken);

            return RecordingResult(
                fileName,
                201,
                "Created",
                $"Existing file was renamed to {renamedFileName}; new recording was uploaded.",
                fileName);
        }

        await using (var content = recording.OpenReadStream())
        {
            await npcRecordingStorage.UploadRecordingAsync(fileName, content, cancellationToken);
        }

        if (conflictExists)
        {
            return RecordingResult(
                fileName,
                200,
                "OK",
                "File was uploaded and overwrote an existing file with the same name.",
                fileName);
        }

        await contentRepository.InsertRecordingAsync(questId, npcId, line, fileName, cancellationToken);
        return RecordingResult(fileName, 201, "Created", "File was uploaded.", fileName);
    }

    private async Task<UploadNpcRecordingResult> UploadMassNpcRecordingAsync(
        NpcRecordingUpload recording,
        bool overwrite,
        int? overrideQuestId,
        int? overrideNpcId,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(recording.FileName);
        var validation = ValidateMassRecordingFile(fileName, recording.ContentType, recording.Length);
        if (validation is not null)
        {
            return validation;
        }

        var fileParts = SplitMassRecordingFileName(fileName)!.Value;
        var line = ParseRecordingLine(fileName);

        int questId;
        if (overrideQuestId is not null)
        {
            questId = overrideQuestId.Value;
            if (!await contentRepository.QuestExistsAsync(questId, cancellationToken))
            {
                return RecordingResult(fileName, 404, "Not Found", "The selected quest could not be found.");
            }
        }
        else
        {
            var resolvedQuestId = await contentRepository.GetQuestIdByDegeneratedNameAsync(
                fileParts.QuestName,
                cancellationToken);
            if (resolvedQuestId is null)
            {
                return RecordingResult(
                    fileName,
                    404,
                    "Not Found",
                    $"Quest with a name corresponding to {fileParts.QuestName} couldn't be found.");
            }

            questId = resolvedQuestId.Value;
        }

        int npcId;
        if (overrideNpcId is not null)
        {
            npcId = overrideNpcId.Value;
            if (!await contentRepository.QuestNpcLinkExistsAsync(questId, npcId, cancellationToken))
            {
                return RecordingResult(fileName, 404, "Not Found", "The selected NPC could not be found for this quest.");
            }
        }
        else
        {
            var resolvedNpcId = await contentRepository.GetQuestNpcIdByDegeneratedNameAsync(
                questId,
                fileParts.NpcName,
                cancellationToken);
            if (resolvedNpcId is null)
            {
                return RecordingResult(
                    fileName,
                    404,
                    "Not Found",
                    $"NPC with a name corresponding to {fileParts.NpcName} couldn't be found.");
            }

            npcId = resolvedNpcId.Value;
        }

        var storedFileName = fileName;
        if (await contentRepository.RecordingFileBelongsToDifferentRecordingAsync(
                storedFileName,
                questId,
                npcId,
                line,
                cancellationToken))
        {
            return RecordingResult(
                fileName,
                409,
                "Conflict",
                "A recording with this filename is already connected to a different quest, NPC, or line.");
        }

        var conflictExists = await npcRecordingStorage.RecordingExistsAsync(storedFileName, cancellationToken);
        if (conflictExists && !overwrite)
        {
            storedFileName = CreateConflictRecordingFileName(storedFileName);
        }

        await using (var content = recording.OpenReadStream())
        {
            await npcRecordingStorage.UploadRecordingAsync(storedFileName, content, cancellationToken);
        }

        if (conflictExists && overwrite)
        {
            return RecordingResult(
                fileName,
                200,
                "OK",
                "File was uploaded and overwrote an existing file with the same name.",
                storedFileName);
        }

        await contentRepository.InsertRecordingAsync(questId, npcId, line, storedFileName, cancellationToken);
        return RecordingResult(
            fileName,
            201,
            "Created",
            conflictExists
                ? $"File was uploaded and renamed to {storedFileName} in order to prevent a conflict."
                : "File was uploaded.",
            storedFileName);
    }

    private static UploadNpcRecordingResult? ValidateRecordingFile(string fileName, string contentType, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return RecordingResult(fileName, 400, "Bad Request", "A filename is required.");
        }

        if (length == 0)
        {
            return RecordingResult(fileName, 400, "Bad Request", "Recording file cannot be empty.");
        }

        if (new StringInfo(fileName).LengthInTextElements > RecordingFileNameMaxLength)
        {
            return RecordingResult(
                fileName,
                400,
                "Bad Request",
                $"Filename cannot be longer than {RecordingFileNameMaxLength} characters.");
        }

        if (!string.Equals(Path.GetExtension(fileName), ".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingResult(fileName, 415, "Unsupported Media Type", "Only .ogg files are accepted.");
        }

        var normalizedContentType = NormalizeContentType(contentType);
        if (string.IsNullOrWhiteSpace(normalizedContentType) ||
            !AcceptedRecordingContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            return RecordingResult(
                fileName,
                415,
                "Unsupported Media Type",
                $"The uploaded file is not an accepted OGG content type. MIME-type {contentType} provided.");
        }

        if (!TryParseRecordingLine(fileName, out _))
        {
            return RecordingResult(
                fileName,
                400,
                "Bad Request",
                "Filename must end with a supported line number, for example 1.ogg or quest-npc-1.ogg.");
        }

        return null;
    }

    private static UploadNpcRecordingResult? ValidateMassRecordingFile(
        string fileName,
        string contentType,
        long length)
    {
        var validation = ValidateRecordingFile(fileName, contentType, length);
        if (validation is not null)
        {
            return validation;
        }

        if (SplitMassRecordingFileName(fileName) is not { } fileParts ||
            string.IsNullOrWhiteSpace(fileParts.QuestName) ||
            string.IsNullOrWhiteSpace(fileParts.NpcName) ||
            string.IsNullOrWhiteSpace(fileParts.Line))
        {
            return RecordingResult(
                fileName,
                400,
                "Bad Request",
                "File doesn't follow the required format (questname-npcname-line.ogg).");
        }

        return null;
    }

    private static (string QuestName, string NpcName, string Line)? SplitMassRecordingFileName(string fileName)
    {
        var parts = fileName.Split('-');
        if (parts.Length != 3)
        {
            return null;
        }

        return (parts[0], parts[1], Path.GetFileNameWithoutExtension(parts[2]));
    }

    private static string CreateConflictRecordingFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        const string extension = ".ogg";
        var suffix = FormattableString.Invariant($"_{Guid.NewGuid():N}");
        var maxStemLength = RecordingFileNameMaxLength - suffix.Length - extension.Length;
        var safeStem = new StringInfo(stem).LengthInTextElements <= maxStemLength
            ? stem
            : new StringInfo(stem).SubstringByTextElements(0, maxStemLength);

        return FormattableString.Invariant($"{safeStem}{suffix}{extension}");
    }

    private static string CreateArchivedRecordingFileName(string fileName, int recordingId, string archiveDate)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        const string extension = ".ogg";
        var prefix = FormattableString.Invariant($"!archived_{archiveDate}_{recordingId}_");
        var maxStemLength = RecordingFileNameMaxLength - prefix.Length - extension.Length;
        var safeStem = maxStemLength > 0 && new StringInfo(stem).LengthInTextElements > maxStemLength
            ? new StringInfo(stem).SubstringByTextElements(0, maxStemLength)
            : stem;

        return FormattableString.Invariant($"{prefix}{safeStem}{extension}");
    }

    private static int ParseRecordingLine(string fileName)
    {
        TryParseRecordingLine(fileName, out var line);
        return line;
    }

    private static bool TryParseRecordingLine(string fileName, out int line)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var lineText = RecordingLineSuffixRegex().Match(stem) is { Success: true } match
            ? match.Groups["line"].Value
            : stem;

        return int.TryParse(lineText, NumberStyles.None, CultureInfo.InvariantCulture, out line) &&
            line >= RecordingLineMinValue &&
            line <= RecordingLineMaxValue;
    }

    private static string NormalizeContentType(string contentType) =>
        contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];

    private static UploadNpcRecordingResult RecordingResult(
        string fileName,
        int code,
        string message,
        string description,
        string? storedFileName = null) =>
        new(fileName, code, message, description, storedFileName);

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
        options.Select(option => new ContentOptionResponse(
            option.Id,
            option.Name,
            option.VoiceActorName)).ToArray();

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

    [GeneratedRegex("-(?<line>\\d+)$")]
    private static partial Regex RecordingLineSuffixRegex();
}
