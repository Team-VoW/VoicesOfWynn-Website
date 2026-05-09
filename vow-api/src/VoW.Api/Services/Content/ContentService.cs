using System.Text.RegularExpressions;
using System.Globalization;
using MySqlConnector;
using VoW.Api.Contracts.Content;
using VoW.Api.Domain.Content;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Content;

public sealed partial class ContentService(IContentRepository contentRepository) : IContentService
{
    private const int ContentNameMaxLength = 63;

    public async Task<ContentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var quests = await contentRepository.GetQuestsAsync(cancellationToken);
        var writers = await GetUsersByRoleAsync(ContentUserRole.Writer, cancellationToken);
        var voiceActors = await GetUsersByRoleAsync(ContentUserRole.VoiceActor, cancellationToken);
        var soundEditors = await GetUsersByRoleAsync(ContentUserRole.SoundEditor, cancellationToken);

        return new ContentOptionsResponse(
            ToResponse(quests),
            ToResponse(writers),
            ToResponse(voiceActors),
            ToResponse(soundEditors));
    }

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
