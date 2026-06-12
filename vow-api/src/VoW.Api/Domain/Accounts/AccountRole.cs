namespace VoW.Api.Domain.Accounts;

public sealed class AccountRole
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public int Weight { get; set; }
}
