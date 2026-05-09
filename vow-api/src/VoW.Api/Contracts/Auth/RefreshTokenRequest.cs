using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Auth;

public sealed record RefreshTokenRequest(
    [Required] string RefreshToken);
