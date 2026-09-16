using VoW.Api.Contracts.Reports;
using VoW.Api.Domain.Reports;
using VoW.Api.Repositories;
using VoW.Api.Services.Reports;
using Xunit;

namespace VoW.Api.Tests;

public sealed class LineReportTests
{
    [Fact]
    public async Task AFirstReportIsCreatedAndARepeatIsFoldedIntoIt()
    {
        var (service, reports, _) = Build();

        var first = await service.SubmitAsync(Submission(), "203.0.113.1", default);
        var second = await service.SubmitAsync(Submission(), "203.0.113.2", default);

        Assert.True(first.Created);
        Assert.Equal(1, first.Response!.ReportedTimes);
        Assert.False(second.Created);
        Assert.Equal(2, second.Response!.ReportedTimes);
        Assert.Single(reports.Rows);
    }

    [Fact]
    public async Task AnAnonymousReportStoresNoTraceOfTheReporter()
    {
        var (service, reports, _) = Build();

        // The PHP version put an unsalted sha256 of the caller's IP in the player column.
        await service.SubmitAsync(Submission(playerName: null), "203.0.113.1", default);

        Assert.Equal("anonymous", reports.Rows.Single().PlayerName);
    }

    [Fact]
    public async Task OutOfRangeCoordinatesAreRejectedBeforeTheDatabaseSeesThem()
    {
        var (service, _, _) = Build();

        var result = await service.SubmitAsync(
            Submission(position: new PositionRequest(9_000_000, 64, 0)),
            "203.0.113.1",
            default);

        Assert.False(result.Succeeded);
        Assert.Contains("Position", result.Errors.Keys);
    }

    [Fact]
    public async Task TheHourlyBudgetIsEnforcedPerAddress()
    {
        var (service, _, limits) = Build();
        limits.Limit = 2;

        for (var i = 0; i < 2; i++)
        {
            Assert.True((await service.SubmitAsync(Submission($"line {i}"), "203.0.113.1", default)).Succeeded);
        }

        var blocked = await service.SubmitAsync(Submission("line 3"), "203.0.113.1", default);
        Assert.True(blocked.IsRateLimited);

        // A different caller has its own budget.
        Assert.True((await service.SubmitAsync(Submission("line 4"), "203.0.113.9", default)).Succeeded);
    }

    /// <summary>
    /// The PHP /active endpoint read $_GET['youngerThan'] behind an isset($_GET['youngerthan'])
    /// guard, so the value was always missing, createFromFormat returned false, and calling
    /// setTime on it was a fatal error rather than a 400.
    /// </summary>
    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2025-13-01")]
    [InlineData("01-01-2025")]
    public async Task AMalformedSinceIsAValidationFailure(string since)
    {
        var (service, _, _) = Build();

        var result = await service.QueryAsync(
            new LineQueryRequest { Statuses = ["accepted"], Since = since },
            default);

        Assert.False(result.Succeeded);
        Assert.Contains("Since", result.Errors.Keys);
    }

    [Fact]
    public async Task AnUnknownStatusIsRejected()
    {
        var (service, _, _) = Build();

        var result = await service.QueryAsync(new LineQueryRequest { Statuses = ["voiced"] }, default);

        Assert.False(result.Succeeded);
        Assert.Contains("Statuses", result.Errors.Keys);
    }

    [Fact]
    public async Task NoStatusAtAllIsRejectedRatherThanReturningEverything()
    {
        var (service, _, _) = Build();

        Assert.False((await service.QueryAsync(new LineQueryRequest(), default)).Succeeded);
    }

    [Fact]
    public async Task TheStatusSetSelectsTheLines()
    {
        var (service, reports, _) = Build();
        reports.Rows.Add(new FakeReportRow("accepted line", "Aledar", null, "accepted", 1, DateTime.UtcNow, "p"));
        reports.Rows.Add(new FakeReportRow("rejected line", "Aledar", null, "rejected", 1, DateTime.UtcNow, "p"));
        reports.Rows.Add(new FakeReportRow("new line", "Aledar", null, "unprocessed", 1, DateTime.UtcNow, "p"));

        var accepted = await service.QueryAsync(new LineQueryRequest { Statuses = ["accepted"] }, default);
        var active = await service.QueryAsync(
            new LineQueryRequest { Statuses = ["accepted", "forwarded", "unprocessed"] },
            default);

        Assert.Equal(["accepted line"], accepted.Response!.Results.Select(line => line.ChatMessage));
        Assert.Equal(2, active.Response!.Results.Count);
    }

    [Fact]
    public async Task SettingAStatusUpdatesKnownLinesAndInsertsUnknownOnes()
    {
        var (service, reports, _) = Build();
        reports.Rows.Add(new FakeReportRow("known", null, null, "unprocessed", 1, DateTime.UtcNow, "p"));

        var result = await service.SetStatusAsync(
            new SetLineStatusRequest { ChatMessages = ["known", "unknown"], Status = "fixed" },
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Response!.Updated);
        Assert.Equal(1, result.Response.Inserted);
        Assert.All(reports.Rows, row => Assert.Equal("fixed", row.Status));
    }

    [Fact]
    public async Task SettingAnInvalidStatusIsRejected()
    {
        var (service, _, _) = Build();

        var result = await service.SetStatusAsync(
            new SetLineStatusRequest { ChatMessages = ["a"], Status = "voiced" },
            default);

        Assert.False(result.Succeeded);
        Assert.Contains("Status", result.Errors.Keys);
    }

    private static SubmitLineReportRequest Submission(
        string chatMessage = "[1/1] Aledar: Hello there.",
        string? playerName = "Kmaxi",
        PositionRequest? position = null) => new()
    {
        ChatMessage = chatMessage,
        NpcName = "Aledar",
        PlayerName = playerName,
        Position = position ?? new PositionRequest(10, 64, -20),
    };

    private static (LineReportService Service, MemoryReports Reports, MemoryWriteLimits Limits) Build()
    {
        var reports = new MemoryReports();
        var limits = new MemoryWriteLimits();
        return (new LineReportService(reports, limits), reports, limits);
    }
}

internal sealed record FakeReportRow(
    string ChatMessage,
    string? NpcName,
    ReportPosition? Position,
    string Status,
    int ReportedTimes,
    DateTime TimeSubmitted,
    string PlayerName);

internal sealed class MemoryReports : IReportRepository
{
    public List<FakeReportRow> Rows { get; } = [];

    public Task<LineReportOutcome> CreateOrIncrementAsync(NewLineReport report, CancellationToken ct)
    {
        var index = Rows.FindIndex(row => row.ChatMessage == report.ChatMessage);
        if (index < 0)
        {
            Rows.Add(new FakeReportRow(
                report.ChatMessage, report.NpcName, report.Position, "unprocessed", 1, DateTime.UtcNow, report.PlayerName));
            return Task.FromResult(new LineReportOutcome(true, 1));
        }

        var existing = Rows[index];
        Rows[index] = existing with { ReportedTimes = existing.ReportedTimes + 1 };
        return Task.FromResult(new LineReportOutcome(false, existing.ReportedTimes + 1));
    }

    public Task<LineQueryPage> QueryLinesAsync(LineQueryCriteria criteria, CancellationToken ct)
    {
        var matches = Rows
            .Where(row => criteria.Statuses.Contains(row.Status))
            .Where(row => criteria.Npc is null || row.NpcName == criteria.Npc)
            .Where(row => row.ReportedTimes >= criteria.MinReports)
            .Where(row => criteria.Since is null || DateOnly.FromDateTime(row.TimeSubmitted) >= criteria.Since)
            .ToList();

        return Task.FromResult(new LineQueryPage(
            matches.Count,
            matches.Skip(criteria.Offset).Take(criteria.Limit)
                .Select(row => new LineReportLine(row.ChatMessage, row.NpcName, row.Position)).ToList()));
    }

    public Task<LineStatusUpsertCounts> UpsertLineStatusAsync(
        IReadOnlyList<string> chatMessages, string status, int chunkSize, CancellationToken ct)
    {
        var updated = 0;
        var inserted = 0;
        foreach (var message in chatMessages)
        {
            var index = Rows.FindIndex(row => row.ChatMessage == message);
            if (index >= 0)
            {
                Rows[index] = Rows[index] with { Status = status };
                updated++;
            }
            else
            {
                Rows.Add(new FakeReportRow(message, null, null, status, 0, DateTime.UtcNow, "<IMPORT>"));
                inserted++;
            }
        }

        return Task.FromResult(new LineStatusUpsertCounts(updated, inserted));
    }

    public async Task<VoicedLineImportCounts> MarkLinesAsVoicedAsync(
        IReadOnlyList<string> chatMessages, int chunkSize, CancellationToken ct)
    {
        var counts = await UpsertLineStatusAsync(chatMessages, "fixed", chunkSize, ct);
        return new VoicedLineImportCounts(counts.Updated, counts.Inserted);
    }

    public Task<int> DeleteLinesAsync(IReadOnlyList<string> chatMessages, int chunkSize, CancellationToken ct) =>
        Task.FromResult(Rows.RemoveAll(row => chatMessages.Contains(row.ChatMessage)));

    public Task<IReadOnlyDictionary<string, ReportPosition>> GetLastPositionsByNpcNameAsync(
        IReadOnlyList<string> npcNames, CancellationToken ct)
    {
        var positions = new Dictionary<string, ReportPosition>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows.Where(row => row.NpcName is not null && row.Position is not null)
                     .Where(row => npcNames.Contains(row.NpcName!, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(row => row.TimeSubmitted))
        {
            positions[row.NpcName!] = row.Position!;
        }

        return Task.FromResult<IReadOnlyDictionary<string, ReportPosition>>(positions);
    }

    public Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> UpdateStatusAsync(int reportId, string status, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(int reportId, CancellationToken ct) => throw new NotSupportedException();
}
