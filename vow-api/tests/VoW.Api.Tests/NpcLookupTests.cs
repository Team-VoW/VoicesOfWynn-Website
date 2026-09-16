using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Contracts.Content;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Content;
using VoW.Api.Repositories;
using VoW.Api.Services.Content;
using VoW.Api.Services.Storage;
using Xunit;

namespace VoW.Api.Tests;

public sealed class NpcLookupTests
{
    /// <summary>
    /// Sightings are keyed by the degenerated name, because that is the form the mod reports and
    /// the only form report.npc_name holds - verified against production data, where all 102k
    /// report rows are lowercase and alphanumeric. Looking a display name up instead silently
    /// resolves nothing for any NPC whose name has a space in it.
    /// </summary>
    [Theory]
    [InlineData("Tasim the Blacksmith", "tasimtheblacksmith")]
    [InlineData("Ragni's Guard", "ragnisguard")]
    [InlineData("Aledar", "aledar")]
    public async Task ASightingIsResolvedByTheDegeneratedName(string name, string degeneratedName)
    {
        var content = new MemoryContent();
        var reports = new MemoryReports();
        content.Npcs.Add(new NpcSearchMatch(1, name, degeneratedName));
        reports.Rows.Add(
            new FakeReportRow("a line", degeneratedName, new(10, 64, -20), "unprocessed", 1, DateTime.UtcNow, "p"));

        var response = await Build(content, reports).SearchAsync(Request(name[..3]), default);

        var result = Assert.Single(response.Results);
        Assert.Equal(name, result.Name);
        Assert.Equal(new NpcLastSeenResponse(10, 64, -20), result.LastSeenAt);
    }

    /// <summary>A report filed under the display name is not what the mod sends, and must not match.</summary>
    [Fact]
    public async Task ASightingFiledUnderTheDisplayNameIsNotMistakenForOne()
    {
        var content = new MemoryContent();
        var reports = new MemoryReports();
        content.Npcs.Add(new NpcSearchMatch(1, "Tasim the Blacksmith", "tasimtheblacksmith"));
        reports.Rows.Add(new FakeReportRow(
            "a line", "Tasim the Blacksmith", new(10, 64, -20), "unprocessed", 1, DateTime.UtcNow, "p"));

        var response = await Build(content, reports).SearchAsync(Request("Tasim"), default);

        Assert.Null(Assert.Single(response.Results).LastSeenAt);
    }

    [Fact]
    public async Task AnNpcNobodyHasReportedHasNoLastSighting()
    {
        var content = new MemoryContent();
        content.Npcs.Add(new NpcSearchMatch(1, "Aledar", "aledar"));

        var response = await Build(content, new MemoryReports()).SearchAsync(Request("Ale"), default);

        Assert.Null(Assert.Single(response.Results).LastSeenAt);
    }

    /// <summary>
    /// PHP applied the SQL LIMIT first and filtered afterwards, so asking for a page of NPCs
    /// without pictures could return anything from zero to the limit with no way to reach the rest.
    /// </summary>
    [Fact]
    public async Task TheMissingPictureFilterRunsBeforeTheLimit()
    {
        var content = new MemoryContent();
        var storage = new CountingImageStorage();
        for (var i = 1; i <= 20; i++)
        {
            content.Npcs.Add(new NpcSearchMatch(i, $"Guard {i:00}", $"guard{i:00}"));
            if (i % 2 == 0) storage.NpcIdsWithImages.Add(i);
        }

        var request = Request("Guard");
        request = new NpcLookupRequest { Q = request.Q, Limit = 5, MissingPicture = true };
        var response = await Build(content, new MemoryReports(), storage).SearchAsync(request, default);

        Assert.Equal(5, response.Results.Count);
        Assert.All(response.Results, result => Assert.True(result.NpcId % 2 == 1));
    }

    /// <summary>
    /// Asking the blob store per NPC is what made the legacy filter issue up to 500 sequential
    /// storage round trips in a single request.
    /// </summary>
    [Fact]
    public async Task TheImageListingIsReadOncePerSearch()
    {
        var content = new MemoryContent();
        var storage = new CountingImageStorage();
        for (var i = 1; i <= 50; i++) content.Npcs.Add(new NpcSearchMatch(i, $"Guard {i:00}", $"guard{i:00}"));

        var request = new NpcLookupRequest { Q = "Guard", Limit = 50, MissingPicture = true };
        await Build(content, new MemoryReports(), storage).SearchAsync(request, default);

        Assert.Equal(1, storage.ListCalls);
    }

    [Fact]
    public async Task AnEmptyQueryReturnsNothingRatherThanEveryNpc()
    {
        var content = new MemoryContent();
        content.Npcs.Add(new NpcSearchMatch(1, "Aledar", "aledar"));

        var response = await Build(content, new MemoryReports())
            .SearchAsync(new NpcLookupRequest { Q = "   " }, default);

        Assert.Empty(response.Results);
    }

    private static NpcLookupRequest Request(string q) => new() { Q = q };

    private static NpcLookupService Build(
        MemoryContent content,
        MemoryReports reports,
        INpcImageStorage? storage = null) =>
        new(content, reports, storage ?? new CountingImageStorage(), new MemoryCache(new MemoryCacheOptions()));
}

internal sealed class CountingImageStorage : INpcImageStorage
{
    public HashSet<int> NpcIdsWithImages { get; } = [];

    public int ListCalls { get; private set; }

    public Task<IReadOnlySet<int>> ListNpcIdsWithImagesAsync(CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlySet<int>>(NpcIdsWithImages);
    }

    public Uri GetImageUrl(int npcId) => new($"https://storage.test/npcs/{npcId}.webp");

    public Uri GetDefaultImageUrl() => new("https://storage.test/npcs/default.webp");

    public Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> CopyImageIfExistsAsync(int source, int destination, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class MemoryContent : IContentRepository
{
    public List<NpcSearchMatch> Npcs { get; } = [];

    public Task<IReadOnlyList<NpcSearchMatch>> SearchNpcsByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NpcSearchMatch>>(Npcs
            .Where(npc => npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(npc => npc.Name, StringComparer.Ordinal)
            .Take(limit)
            .ToList());

    public Task<IReadOnlyCollection<ContentOption>> GetQuestsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<ContentOption>> GetNpcsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<ContentOption>> GetUsersByRolesAsync(IReadOnlyCollection<DiscordRoleId> roles, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestDegeneratedNameExistsAsync(string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestDegeneratedNameExistsAsync(int exceptQuestId, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestExistsAsync(int questId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<NpcArchiveData?> GetNpcArchiveDataAsync(int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string?> GetQuestDegeneratedNameAsync(int questId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string?> GetNpcDegeneratedNameAsync(int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int?> GetQuestIdByDegeneratedNameAsync(string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int?> GetQuestNpcIdByDegeneratedNameAsync(int questId, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> NpcDegeneratedNameConflictsForLinkedQuestsAsync(int npcId, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> NpcDegeneratedNameConflictsInQuestAsync(int questId, int npcId, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestHasNpcsAsync(int questId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestNpcLinkExistsAsync(int questId, int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> QuestNpcHasRecordingsAsync(int questId, int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<NpcRecording>> GetQuestNpcRecordingsAsync(int questId, int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<RecordingFile?> GetQuestNpcRecordingFileAsync(int questId, int npcId, int recordingId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<RecordingFile?> GetRecordingByFileAsync(string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<RecordingConflict?> GetRecordingFileConflictAsync(string fileName, int questId, int npcId, int line, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateRecordingFileAsync(int recordingId, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<CreatedContent> InsertRecordingAsync(int questId, int npcId, int line, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> DeleteQuestNpcRecordingAsync(int questId, int npcId, int recordingId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ContentSearchPage> SearchAsync(ContentSearchCriteria criteria, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<CreatedContent> CreateQuestAsync(CreateQuestCommand command, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<CreatedContent> CreateNpcAsync(CreateNpcCommand command, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateQuestAsync(int questId, string name, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateQuestWriterAsync(int questId, int? writerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> DeleteQuestAsync(int questId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateNpcAsync(int npcId, string name, string degeneratedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateNpcVoiceActorAsync(int npcId, int? voiceActorUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> LinkNpcToQuestAsync(int questId, int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UpdateQuestNpcSoundEditorAsync(int questId, int npcId, int? soundEditorUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> UnlinkNpcFromQuestAsync(int questId, int npcId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int?> ArchiveNpcAsync(int npcId, bool createReplacement, IReadOnlyCollection<ArchivedRecordingFile> archivedRecordings, IReadOnlyCollection<int> deletedRecordingIds, CancellationToken cancellationToken) => throw new NotSupportedException();

}
