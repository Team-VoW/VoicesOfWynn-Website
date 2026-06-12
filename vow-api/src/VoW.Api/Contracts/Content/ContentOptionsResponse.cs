namespace VoW.Api.Contracts.Content;

public sealed record ContentOptionsResponse(
    IReadOnlyCollection<ContentOptionResponse> Quests,
    IReadOnlyCollection<ContentOptionResponse> Npcs,
    IReadOnlyCollection<ContentOptionResponse> Writers,
    IReadOnlyCollection<ContentOptionResponse> VoiceActors,
    IReadOnlyCollection<ContentOptionResponse> SoundEditors);
