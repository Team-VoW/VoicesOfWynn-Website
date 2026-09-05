using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Feedback;

public sealed record QuestRatingRequest(
    [Required] Guid SubmissionId,
    [Required] Guid InstallationId,
    [Required, StringLength(200, MinimumLength = 1)] string QuestName,
    [Range(1, 5)] int Score,
    [Required, StringLength(64, MinimumLength = 1)] string ModVersion);

public sealed record QuestCommentRequest(
    [Required, StringLength(128, MinimumLength = 32)] string EditToken,
    [Required, StringLength(2000, MinimumLength = 1)] string Comment);

public sealed record QuestRatingResponse(string Id, string EditToken);

public sealed class QuestFeedbackQuery
{
    [StringLength(200)] public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 1000000)] public int MinimumCount { get; set; } = 1;
    [RegularExpression("^(lowest|highest)$")] public string Sort { get; set; } = "lowest";
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    public bool CommentsOnly { get; set; }
}

public sealed record FeedbackPage<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);
public sealed class QuestFeedbackSummary
{
    public string GroupingKey { get; set; } = "";
    public string QuestName { get; set; } = "";
    public bool Unmatched { get; set; }
    public long UnmatchedCount { get; set; }
    public double AverageScore { get; set; }
    public long RatingCount { get; set; }
    public long CommentCount { get; set; }
    public DateTime LatestSubmission { get; set; }
}
public sealed class QuestFeedbackItem
{
    public string QuestName { get; set; } = "";
    public bool Unmatched { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public string ModVersion { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
public sealed class ScoreCount
{
    public int Score { get; set; }
    public long Count { get; set; }
}
public sealed record QuestFeedbackDetail(IReadOnlyList<ScoreCount> Distribution, FeedbackPage<QuestFeedbackItem> Feedback);
