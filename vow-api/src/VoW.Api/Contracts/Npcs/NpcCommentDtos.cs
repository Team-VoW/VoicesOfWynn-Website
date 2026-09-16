namespace VoW.Api.Contracts.Npcs;

public sealed record NpcCommentResponse(
    int CommentId,
    bool Verified,
    string AuthorName,
    string? AvatarUrl,
    string Content,
    // Null for comments posted before the created_at column existed.
    DateTime? CreatedAt,
    /* Whether the caller may delete this comment, so the client need not re-derive the rule. */
    bool CanDelete);

public sealed record NpcCommentsResponse(IReadOnlyCollection<NpcCommentResponse> Comments);

public sealed record PostNpcCommentRequest(string? Name, string? Email, string Content);
