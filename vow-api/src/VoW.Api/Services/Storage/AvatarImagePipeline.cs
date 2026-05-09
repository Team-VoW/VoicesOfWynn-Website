using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace VoW.Api.Services.Storage;

public static class AvatarImagePipeline
{
    private const int TargetSize = 512;
    private const int WebpQuality = 90;

    public static async Task<MemoryStream> NormalizeToWebpAsync(Stream input, CancellationToken cancellationToken)
    {
        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var image = await Image.LoadAsync(input, cancellationToken);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(TargetSize, TargetSize),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
        }));

        var output = new MemoryStream();
        await image.SaveAsync(output, new WebpEncoder { Quality = WebpQuality }, cancellationToken);
        output.Position = 0;
        return output;
    }
}
