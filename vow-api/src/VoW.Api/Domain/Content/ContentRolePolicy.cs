using VoW.Api.Domain.Auth;

namespace VoW.Api.Domain.Content;

public static class ContentRolePolicy
{
    private static readonly DiscordRoleId[] WriterRoles =
    [
        DiscordRoleId.Writer
    ];

    private static readonly DiscordRoleId[] VoiceActorRoles =
    [
        DiscordRoleId.BeginnerActor,
        DiscordRoleId.AdvancedActor,
        DiscordRoleId.SkilledActor,
        DiscordRoleId.ExpertActor
    ];

    private static readonly DiscordRoleId[] SoundEditorRoles =
    [
        DiscordRoleId.SoundEditor
    ];

    public static IReadOnlyCollection<DiscordRoleId> RolesFor(ContentUserRole role) => role switch
    {
        ContentUserRole.Writer => WriterRoles,
        ContentUserRole.VoiceActor => VoiceActorRoles,
        ContentUserRole.SoundEditor => SoundEditorRoles,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
