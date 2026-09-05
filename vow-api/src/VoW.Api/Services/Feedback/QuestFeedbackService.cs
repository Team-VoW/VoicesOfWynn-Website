using System.Security.Cryptography;
using System.Text;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Feedback;

public sealed record FeedbackWriteResult(int Status, QuestRatingResponse? Rating = null);

public sealed class QuestFeedbackService(IQuestFeedbackRepository repository, IConfiguration configuration)
{
    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private string Token(string id)
    {
        // Stable across retries and replicas; only the hash is stored in the database.
        var secret = configuration["QUEST_FEEDBACK_EDIT_SECRET"];
        if (string.IsNullOrEmpty(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("QUEST_FEEDBACK_EDIT_SECRET must contain at least 32 bytes.");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("quest-feedback:" + id)));
    }

    private Task<bool> Limit(string kind, string value, int limit, CancellationToken ct) =>
        repository.ConsumeLimitAsync(Hash(kind + ":" + value), limit, ct);

    public async Task<FeedbackWriteResult> RateAsync(QuestRatingRequest request, string ip, CancellationToken ct)
    {
        if (request.SubmissionId == Guid.Empty || request.InstallationId == Guid.Empty || request.Score is < 1 or > 5
            || string.IsNullOrWhiteSpace(request.QuestName) || request.QuestName.Length > 200
            || string.IsNullOrWhiteSpace(request.ModVersion) || request.ModVersion.Length > 64)
            return new(400);
        var key = QuestNameNormalizer.Normalize(request.QuestName);
        if (key.Length is 0 or > 200 || key.Any(char.IsControl)) return new(400);
        if (!await Limit("ip", ip, 120, ct) || !await Limit("installation", request.InstallationId.ToString(), 30, ct)) return new(429);
        var id = request.SubmissionId.ToString();
        var token = Token(id);
        await repository.InsertAsync(request, key, Hash(token), ct);
        var stored = await repository.FindAsync(id, ct);
        if (stored is null) throw new InvalidOperationException("Inserted feedback was not found.");
        if (stored.InstallationId != request.InstallationId.ToString() || stored.QuestName != request.QuestName
            || stored.Score != request.Score || stored.ModVersion != request.ModVersion) return new(409);
        // A changed server secret must not return an edit token that cannot edit the rating.
        if (!CryptographicOperations.FixedTimeEquals(Hash(token), stored.EditTokenHash)) return new(409);
        return new(200, new(id, token));
    }

    public async Task<FeedbackWriteResult> CommentAsync(Guid id, QuestCommentRequest request, string ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length > 2000
            || string.IsNullOrEmpty(request.EditToken) || request.EditToken.Length is < 32 or > 128) return new(400);
        if (!await Limit("ip", ip, 120, ct)) return new(429);
        var stored = await repository.FindAsync(id.ToString(), ct);
        if (stored is null || !CryptographicOperations.FixedTimeEquals(Hash(request.EditToken), stored.EditTokenHash)) return new(403);
        if (!await Limit("installation", stored.InstallationId, 30, ct)) return new(429);
        await repository.UpdateCommentAsync(id.ToString(), request.Comment.Trim(), ct);
        return new(204);
    }

    public Task<FeedbackPage<QuestFeedbackSummary>> SummaryAsync(QuestFeedbackQuery query, CancellationToken ct) => repository.SummaryAsync(query, ct);
    public Task<QuestFeedbackDetail> DetailAsync(string key, QuestFeedbackQuery query, CancellationToken ct) => repository.DetailAsync(key, query, ct);
}
