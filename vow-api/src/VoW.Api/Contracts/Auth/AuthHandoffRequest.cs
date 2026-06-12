using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Auth;

public sealed record AuthHandoffRequest(
    [Required] string Code);
