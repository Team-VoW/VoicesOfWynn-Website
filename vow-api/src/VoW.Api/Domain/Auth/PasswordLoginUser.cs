using VoW.Api.Domain.Users;

namespace VoW.Api.Domain.Auth;

public sealed record PasswordLoginUser(
    int UserId,
    string? PasswordHash,
    bool ForcePasswordChange,
    UserProfile Profile);
