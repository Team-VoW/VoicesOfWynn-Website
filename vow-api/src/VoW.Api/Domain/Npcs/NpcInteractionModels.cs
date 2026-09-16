namespace VoW.Api.Domain.Npcs;

public enum VoteType
{
    Up,
    Down
}

/// <summary>A recording of a single voiced line, scoped to the quest it belongs to.</summary>
public sealed record QuestRecording(int RecordingId, int QuestId, short Line, string FileName);

public sealed record QuestName(int QuestId, string Name, string DegeneratedName);

public sealed record NpcVoteCounts(int Upvotes, int Downvotes);

public sealed record NpcComment(
    int CommentId,
    bool Verified,
    int? UserId,
    string AuthorName,
    string? AvatarUrl,
    string Content,
    // Null for comments posted before the created_at column existed.
    DateTime? CreatedAt);

public sealed record NewNpcComment(
    int NpcId,
    int? UserId,
    byte[]? Ip,
    string? Name,
    string? Email,
    string Content);

/// <summary>Who a stored comment belongs to, for permission checks before deleting it.</summary>
public sealed record NpcCommentOwner(int CommentId, int NpcId, int? UserId);
