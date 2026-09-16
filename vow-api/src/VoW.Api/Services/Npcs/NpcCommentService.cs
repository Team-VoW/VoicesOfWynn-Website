using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Controllers;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Npcs;

public sealed class NpcCommentService(
    INpcInteractionRepository repository,
    IAccountRepository accountRepository,
    IWriteLimitRepository writeLimits,
    ICommentNotifier notifier) : INpcCommentService
{
    // Posting a comment fans out to a Discord webhook, so the budgets are deliberately tighter
    // than quest feedback's. The global cap is the backstop: whatever happens to client address
    // resolution, the webhook cannot be turned into an amplifier.
    private const int CommentsPerHourPerVisitor = 10;
    private const int CommentsPerHourPerNpc = 30;
    private const int CommentsPerHourGlobal = 200;

    public async Task<NpcCommentsResponse?> GetCommentsAsync(
        int npcId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!await repository.NpcExistsAsync(npcId, cancellationToken))
        {
            return null;
        }

        var comments = await repository.GetCommentsAsync(npcId, cancellationToken);
        var userId = user.GetUserId();
        var isSystemAdmin = userId is not null
            && await accountRepository.IsSystemAdminAsync(userId.Value, cancellationToken);

        return new NpcCommentsResponse(comments
            .Select(comment => Response(comment, CanDelete(comment.UserId, userId, isSystemAdmin)))
            .ToArray());
    }

    public async Task<NpcWriteResult<NpcCommentResponse>> PostAsync(
        int npcId,
        PostNpcCommentRequest request,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        // Whether a comment counts as a contributor's is decided by the bearer token, never by
        // anything the client asks for.
        if (!NpcCommentValidator.TryValidate(request, userId is not null, out var validated, out var errors))
        {
            return NpcWriteResult<NpcCommentResponse>.Invalid(errors);
        }

        var npcName = await repository.GetNpcNameAsync(npcId, cancellationToken);
        if (npcName is null)
        {
            return NpcWriteResult<NpcCommentResponse>.NotFound();
        }

        var visitorKey = userId is not null ? $"u:{userId}" : ipAddress;
        if (!await Limit("comment-visitor", visitorKey, CommentsPerHourPerVisitor, cancellationToken)
            || !await Limit("comment-npc", npcId.ToString(), CommentsPerHourPerNpc, cancellationToken)
            || !await Limit("comment-global", string.Empty, CommentsPerHourGlobal, cancellationToken))
        {
            return NpcWriteResult<NpcCommentResponse>.RateLimited();
        }

        var commentId = await repository.InsertCommentAsync(
            new NewNpcComment(
                npcId,
                userId,
                // Guests are recorded by address so abuse can be traced; contributors are not,
                // because their account already identifies them.
                userId is null ? ParseAddress(ipAddress) : null,
                validated.Name,
                validated.Email,
                validated.Content),
            cancellationToken);

        var stored = await repository.GetCommentAsync(commentId, cancellationToken)
            ?? throw new InvalidOperationException("The stored comment could not be read back.");

        // Not the request's token: the comment is already committed, and a client that has
        // disconnected must not cancel the announcement.
        await notifier.NotifyAsync(
            new CommentNotification(npcId, npcName, stored.AuthorName, stored.AvatarUrl, validated.Content, commentId),
            CancellationToken.None);

        // Guests cannot delete their own comment, since the address rule that used to allow it is
        // gone - so this is computed rather than assumed, or the client would render a button that
        // is refused on click.
        var isSystemAdmin = userId is not null
            && await accountRepository.IsSystemAdminAsync(userId.Value, cancellationToken);
        return NpcWriteResult<NpcCommentResponse>.Success(
            Response(stored, CanDelete(stored.UserId, userId, isSystemAdmin)));
    }

    public async Task<NpcWriteResult<bool>> DeleteAsync(
        int npcId,
        int commentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var owner = await repository.GetCommentOwnerAsync(commentId, cancellationToken);
        if (owner is null || owner.NpcId != npcId)
        {
            return NpcWriteResult<bool>.NotFound();
        }

        var userId = user.GetUserId();
        var isSystemAdmin = userId is not null
            && await accountRepository.IsSystemAdminAsync(userId.Value, cancellationToken);
        if (!CanDelete(owner.UserId, userId, isSystemAdmin))
        {
            return NpcWriteResult<bool>.Forbidden();
        }

        await repository.DeleteCommentAsync(commentId, cancellationToken);
        return NpcWriteResult<bool>.Success(true);
    }

    /// <remarks>
    /// The legacy site also let anyone whose address matched the stored one delete a comment.
    /// That rule is dropped: behind the reverse proxy every guest shares an address, so it would
    /// let any visitor delete any other guest's comment.
    /// </remarks>
    private static bool CanDelete(int? authorId, int? userId, bool isSystemAdmin) =>
        isSystemAdmin || (userId is not null && authorId == userId);

    private Task<bool> Limit(string kind, string value, int limit, CancellationToken cancellationToken) =>
        writeLimits.ConsumeLimitAsync(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}:{value}")),
            limit,
            cancellationToken);

    private static byte[]? ParseAddress(string ipAddress) =>
        IPAddress.TryParse(ipAddress, out var address) ? address.GetAddressBytes() : null;

    private static NpcCommentResponse Response(NpcComment comment, bool canDelete) =>
        new(comment.CommentId,
            comment.Verified,
            comment.AuthorName,
            comment.AvatarUrl,
            comment.Content,
            comment.CreatedAt,
            canDelete);
}
