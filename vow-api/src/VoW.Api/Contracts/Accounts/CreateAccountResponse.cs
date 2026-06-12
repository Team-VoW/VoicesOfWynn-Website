namespace VoW.Api.Contracts.Accounts;

public sealed record CreateAccountResponse(int UserId, string TemporaryPassword);
