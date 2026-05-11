namespace VoW.Api.Services.Storage;

public interface IAccountAvatarStorage
{
    Task UploadAvatarAsync(int userId, Stream webpContent, CancellationToken cancellationToken);

    Task UploadDiscordAvatarAsync(int userId, Stream pngContent, CancellationToken cancellationToken);

    Task DeleteCustomAvatarsAsync(int userId, CancellationToken cancellationToken);
}
