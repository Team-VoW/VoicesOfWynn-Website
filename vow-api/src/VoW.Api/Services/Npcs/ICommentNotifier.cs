namespace VoW.Api.Services.Npcs;

public sealed record CommentNotification(
    int NpcId,
    string NpcName,
    string AuthorName,
    string? AvatarUrl,
    string Content,
    int CommentId);

public interface ICommentNotifier
{
    /// <summary>
    /// Announces a new comment. The comment is already stored by the time this runs, so a failure
    /// here must never fail the request.
    /// </summary>
    Task NotifyAsync(CommentNotification notification, CancellationToken cancellationToken);
}
