using VoW.Api.Contracts.Contents;
using VoW.Api.Domain.Contents;

namespace VoW.Api.Services.Contents;

/// <summary>Shared by the quest and NPC pages, which credit people in the same shape.</summary>
internal static class ContentCreditMapper
{
    public static ContentCreditResponse? Credit(ContentCredit? credit) =>
        credit is null
            ? null
            : new ContentCreditResponse(
                credit.UserId,
                credit.DisplayName,
                credit.AvatarUrl,
                credit.DefaultAvatarUrl);
}
