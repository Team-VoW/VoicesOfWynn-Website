using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace VoW.Api.Services.Storage;

public static class AvatarImagePipeline
{
    private const int TargetSize = 512;
    private const int WebpQuality = 90;
    private const int MaxBytes = 8_000_000;
    private const int MaxDimension = 4096;
    private const int MaxPixels = 16_777_216;

    public static async Task<MemoryStream> NormalizeToWebpAsync(Stream input, CancellationToken cancellationToken)
    {
        var imageInput = await PrepareInputAsync(input, cancellationToken);
        await using var preparedInput = ReferenceEquals(imageInput, input) ? null : imageInput;

        var info = await Image.IdentifyAsync(imageInput, cancellationToken);
        if (info.Width > MaxDimension || info.Height > MaxDimension || info.Width * (long)info.Height > MaxPixels)
        {
            throw new InvalidImageContentException("Avatar image dimensions exceed the allowed limits.");
        }

        imageInput.Position = 0;
        using var image = await Image.LoadAsync(imageInput, cancellationToken);
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

    private static async Task<Stream> PrepareInputAsync(Stream input, CancellationToken cancellationToken)
    {
        if (input.CanSeek)
        {
            input.Position = 0;
            if (input.Length > MaxBytes)
            {
                throw new InvalidImageContentException("Avatar image exceeds the allowed byte limit.");
            }

            return input;
        }

        var buffer = new MemoryStream();
        var copyBuffer = new byte[81920];
        var total = 0;
        while (true)
        {
            var read = await input.ReadAsync(copyBuffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaxBytes)
            {
                await buffer.DisposeAsync();
                throw new InvalidImageContentException("Avatar image exceeds the allowed byte limit.");
            }

            await buffer.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        return buffer;
    }
}
