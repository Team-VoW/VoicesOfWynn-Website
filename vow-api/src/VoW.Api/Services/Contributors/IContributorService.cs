using System.Security.Claims;
using VoW.Api.Contracts.Contributors;

namespace VoW.Api.Services.Contributors;

public interface IContributorService
{
    Task<ContributorListResponse> ListAsync(ContributorSearchRequest request, CancellationToken cancellationToken);

    Task<ContributorDetailResponse?> GetAsync(int userId, CancellationToken cancellationToken);

    Task<ContributorVotesResponse?> GetVotesAsync(
        int userId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);
}
