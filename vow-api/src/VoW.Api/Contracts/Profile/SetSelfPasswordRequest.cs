namespace VoW.Api.Contracts.Profile;

public sealed record SetSelfPasswordRequest(
    string? OldPassword,
    string? NewPassword,
    string? ConfirmNewPassword);
