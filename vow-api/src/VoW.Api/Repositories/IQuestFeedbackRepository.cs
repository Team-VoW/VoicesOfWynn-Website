using VoW.Api.Contracts.Feedback;

namespace VoW.Api.Repositories;

public interface IQuestFeedbackRepository
{
    Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct);
    Task<StoredQuestRating?> FindAsync(string id, CancellationToken ct);
    Task InsertAsync(QuestRatingRequest request, string key, byte[] hash, CancellationToken ct);
    Task UpdateCommentAsync(string id, string comment, CancellationToken ct);
    Task<FeedbackPage<QuestFeedbackSummary>> SummaryAsync(QuestFeedbackQuery query, CancellationToken ct);
    Task<QuestFeedbackDetail> DetailAsync(string key, QuestFeedbackQuery query, CancellationToken ct);
}
