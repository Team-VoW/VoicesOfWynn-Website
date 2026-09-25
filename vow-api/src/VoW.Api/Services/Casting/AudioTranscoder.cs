using System.Globalization;
using VoW.Api.Services.Tools;

namespace VoW.Api.Services.Casting;

public interface IAudioTranscoder
{
    /// <summary>Converts any audio ffmpeg understands to MP3. Throws <see cref="AudioTranscodeException"/> on bad input.</summary>
    Task<TranscodedAudio> ToMp3Async(Stream input, CancellationToken cancellationToken);
}

public sealed class AudioTranscodeException(string message) : Exception(message);

/// <summary>An MP3 on local disk that is removed when disposed.</summary>
public sealed class TranscodedAudio(string path, double? durationSeconds) : IDisposable
{
    public double? DurationSeconds { get; } = durationSeconds;

    public Stream OpenRead() => File.OpenRead(path);

    public void Dispose() => ExternalProcess.TryDelete(path);
}

public sealed class AudioTranscoder(IConfiguration configuration) : IAudioTranscoder
{
    private readonly string ffmpegPath = configuration["AudioAnalysis:FFmpegPath"] ?? "ffmpeg";
    private readonly string ffprobePath = configuration["AudioAnalysis:FFprobePath"] ?? "ffprobe";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public async Task<TranscodedAudio> ToMp3Async(Stream input, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"casting-{Guid.NewGuid():N}.in");
        var targetPath = Path.Combine(Path.GetTempPath(), $"casting-{Guid.NewGuid():N}.mp3");

        try
        {
            await using (var file = File.Create(sourcePath))
            {
                await input.CopyToAsync(file, cancellationToken);
            }

            // Mono 128k keeps voice clips small while staying transparent for judging a performance.
            var result = await ExternalProcess.RunAsync(
                ffmpegPath,
                ["-hide_banner", "-nostdin", "-y", "-i", sourcePath, "-vn", "-ac", "1", "-codec:a", "libmp3lame", "-b:a", "128k", targetPath],
                Timeout,
                cancellationToken);

            if (result.ExitCode != 0 || !File.Exists(targetPath))
            {
                ExternalProcess.TryDelete(targetPath);
                throw new AudioTranscodeException($"The file could not be read as audio: {ExternalProcess.TrimForError(result.Error)}");
            }

            return new TranscodedAudio(targetPath, await TryGetDurationAsync(targetPath, cancellationToken));
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            ExternalProcess.TryDelete(targetPath);
            throw new AudioTranscodeException(ex.Message);
        }
        finally
        {
            ExternalProcess.TryDelete(sourcePath);
        }
    }

    private async Task<double?> TryGetDurationAsync(string path, CancellationToken cancellationToken)
    {
        var result = await ExternalProcess.RunAsync(
            ffprobePath,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path],
            Timeout,
            cancellationToken);

        return result.ExitCode == 0
               && double.TryParse(result.Output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
               && duration > 0
            ? Math.Round(duration, 2)
            : null;
    }
}
