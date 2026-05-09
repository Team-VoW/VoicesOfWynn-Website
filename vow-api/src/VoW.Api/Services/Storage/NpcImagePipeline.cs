using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace VoW.Api.Services.Storage;

public static class NpcImagePipeline
{
    private const int TargetSize = 256;
    private const int WebpQuality = 85;

    /// <summary>
    /// Normalizes image content to a cropped WebP image.
    /// </summary>
    /// <remarks>
    /// The caller retains ownership of <paramref name="input"/> and must dispose it.
    /// The returned <see cref="MemoryStream"/> is owned by the caller and must be disposed when no longer needed.
    /// If <paramref name="input"/> supports seeking, it is rewound before decoding.
    /// </remarks>
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
