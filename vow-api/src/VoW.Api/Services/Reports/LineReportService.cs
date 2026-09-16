using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VoW.Api.Contracts.Reports;
using VoW.Api.Domain.Reports;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Reports;

/// <summary>
/// Unvoiced line reports: ingestion from the mod client and the listings the Discord bot reads.
/// </summary>
public sealed class LineReportService(
    IReportRepository reportRepository,
    IWriteLimitRepository writeLimits) : ILineReportService
{
    /// <summary>
    /// A player walking through new content can legitimately hear a lot of unvoiced lines in an
    /// hour; the mod already de-duplicates the last 20 locally. This only has to stop a script.
    /// </summary>
    private const int ReportsPerHourPerAddress = 120;

    private const string AnonymousPlayerName = "anonymous";

    /// <summary>Dapper caches its expanded SQL per parameter count, so chunks are a fixed size.</summary>
    private const int ChunkSize = 500;

    public async Task<LineReportSubmissionResult> SubmitAsync(
        SubmitLineReportRequest request,
        string callerIp,
        CancellationToken cancellationToken)
    {
        var position = request.Position;
        if (position is not null && !(ReportPosition.IsInRange(position.X)
                                      && ReportPosition.IsInRange(position.Y)
                                      && ReportPosition.IsInRange(position.Z)))
        {
            return LineReportSubmissionResult.Invalid(
                nameof(request.Position),
                $"Coordinates must be between {ReportPosition.MinCoordinate} and {ReportPosition.MaxCoordinate}.");
        }

        if (!await writeLimits.ConsumeLimitAsync(
                Hash($"report:{callerIp}"),
                ReportsPerHourPerAddress,
                cancellationToken))
        {
            return LineReportSubmissionResult.RateLimited();
        }

        // The PHP version stored an unsalted sha256 of the reporter's IP in the player column when
        // a report was anonymous. Nothing ever read it back, so it is not carried over.
        var playerName = string.IsNullOrWhiteSpace(request.PlayerName)
            ? AnonymousPlayerName
            : request.PlayerName.Trim();

        var outcome = await reportRepository.CreateOrIncrementAsync(
            new NewLineReport(
                request.ChatMessage,
                string.IsNullOrWhiteSpace(request.NpcName) ? null : request.NpcName.Trim(),
                playerName,
                position is null ? null : new ReportPosition(position.X, position.Y, position.Z)),
            cancellationToken);

        return LineReportSubmissionResult.Success(
            new SubmitLineReportResponse(outcome.ReportedTimes),
            outcome.Created);
    }

    public async Task<LineQueryServiceResult> QueryAsync(
        LineQueryRequest request,
        CancellationToken cancellationToken)
    {
        var statuses = request.Statuses
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (statuses.Count == 0)
        {
            return LineQueryServiceResult.Failure(
                nameof(request.Statuses),
                $"At least one status is required. Valid statuses: {ReportStatus.DisplayList}.");
        }

        if (statuses.FirstOrDefault(status => !ReportStatus.IsValid(status)) is { } invalid)
        {
            return LineQueryServiceResult.Failure(
                nameof(request.Statuses),
                $"'{invalid}' is not a status. Valid statuses: {ReportStatus.DisplayList}.");
        }

        // The PHP endpoint fed an unvalidated string to DateTime::createFromFormat and then called
        // a method on the `false` it returned, which was a fatal error rather than a 400.
        DateOnly? since = null;
        if (!string.IsNullOrWhiteSpace(request.Since))
        {
            if (!DateOnly.TryParseExact(
                    request.Since.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return LineQueryServiceResult.Failure(nameof(request.Since), "Since must be a date in yyyy-MM-dd form.");
            }

            since = parsed;
        }

        var page = await reportRepository.QueryLinesAsync(
            new LineQueryCriteria(
                string.IsNullOrWhiteSpace(request.Npc) ? null : request.Npc.Trim(),
                statuses,
                request.MinReports,
                since,
                request.Limit,
                request.Offset),
            cancellationToken);

        return LineQueryServiceResult.Success(new LineQueryResponse(
            page.Total,
            page.Results.Select(line => new LineResponse(
                line.ChatMessage,
                line.NpcName,
                line.Position is null ? null : new PositionResponse(line.Position.X, line.Position.Y, line.Position.Z)))
                .ToList()));
    }

    public async Task<LineStatusServiceResult> SetStatusAsync(
        SetLineStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ReportStatus.IsValid(request.Status))
        {
            return LineStatusServiceResult.Failure(
                nameof(request.Status),
                $"Status must be one of {ReportStatus.DisplayList}.");
        }

        var messages = Distinct(request.ChatMessages);
        if (messages.Count == 0)
        {
            return LineStatusServiceResult.Failure(
                nameof(request.ChatMessages),
                "At least one chat message is required.");
        }

        var counts = await reportRepository.UpsertLineStatusAsync(
            messages,
            request.Status.ToLowerInvariant(),
            ChunkSize,
            cancellationToken);

        return LineStatusServiceResult.Success(new SetLineStatusResponse(counts.Updated, counts.Inserted));
    }

    public async Task<DeleteLinesResponse> DeleteAsync(
        DeleteLinesRequest request,
        CancellationToken cancellationToken)
    {
        var messages = Distinct(request.ChatMessages);
        if (messages.Count == 0)
        {
            return new DeleteLinesResponse(0);
        }

        return new DeleteLinesResponse(
            await reportRepository.DeleteLinesAsync(messages, ChunkSize, cancellationToken));
    }

    private static List<string> Distinct(IReadOnlyList<string> chatMessages) =>
        chatMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
