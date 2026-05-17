using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoW.Api.Services.Tools;

public sealed partial class AudioAnalysisService(IConfiguration configuration, ILogger<AudioAnalysisService> logger) : IAudioAnalysisService
{
    private const double TailDurationToleranceSeconds = 0.05;

    private readonly string ffmpegPath = configuration["AudioAnalysis:FFmpegPath"] ?? "ffmpeg";
    private readonly string ffprobePath = configuration["AudioAnalysis:FFprobePath"] ?? "ffprobe";
    private readonly double silenceNoiseThresholdDb = GetDouble(configuration, "AudioAnalysis:SilenceNoiseThresholdDb", -50);
    private readonly TimeSpan processTimeout = TimeSpan.FromSeconds(GetDouble(configuration, "AudioAnalysis:ProcessTimeoutSeconds", 30));

    public async Task<AudioAnalysisOutcome> AnalyzeAsync(Stream wavStream, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try
        {
            await using (var file = File.Create(tempPath))
            {
                await wavStream.CopyToAsync(file, cancellationToken);
            }

            var duration = await GetDurationAsync(tempPath, cancellationToken);
            var channelMode = await GetChannelModeAsync(tempPath, cancellationToken);
            var integratedLufs = await GetIntegratedLufsAsync(tempPath, cancellationToken);
            var silence = await GetHeadAndTailSilenceAsync(tempPath, duration, cancellationToken);

            return new AudioAnalysisOutcome(
                Success: true,
                IntegratedLufs: integratedLufs,
                LeadingSilenceSeconds: silence.LeadingSeconds,
                TrailingSilenceSeconds: silence.TrailingSeconds,
                ChannelMode: channelMode,
                Error: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AudioAnalysisOutcome(false, null, null, null, null, "Analysis was cancelled.");
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Audio analysis timed out.");
            return new AudioAnalysisOutcome(false, null, null, null, null, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "Audio analysis failed.");
            return new AudioAnalysisOutcome(false, null, null, null, null, ex.Message);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private async Task<double> GetDurationAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            ffprobePath,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed while reading duration: {TrimForError(result.Error)}");
        }

        var text = result.Output.Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration <= 0)
        {
            throw new InvalidOperationException("ffprobe did not return a valid audio duration.");
        }

        return duration;
    }

    private async Task<string> GetChannelModeAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            ffprobePath,
            ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=channels", "-of", "default=noprint_wrappers=1:nokey=1", path],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed while reading channel count: {TrimForError(result.Error)}");
        }

        return result.Output.Trim() switch
        {
            "1" => "mono",
            "2" => "stereo",
            _ => "unknown",
        };
    }

    private async Task<double> GetIntegratedLufsAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            ffmpegPath,
            ["-hide_banner", "-nostdin", "-i", path, "-filter_complex", "ebur128", "-f", "null", "-"],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed while measuring loudness: {TrimForError(result.Error)}");
        }

        var matches = IntegratedLufsRegex().Matches(result.Error);
        foreach (Match match in matches.Cast<Match>().Reverse())
        {
            if (double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lufs))
            {
                return lufs;
            }
        }

        throw new InvalidOperationException("ffmpeg did not return an integrated LUFS value.");
    }

    private async Task<(double? LeadingSeconds, double? TrailingSeconds)> GetHeadAndTailSilenceAsync(
        string path,
        double duration,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            ffmpegPath,
            [
                "-hide_banner",
                "-nostdin",
                "-i",
                path,
                "-af",
                $"silencedetect=noise={silenceNoiseThresholdDb.ToString(CultureInfo.InvariantCulture)}dB:d=0.001",
                "-f",
                "null",
                "-"
            ],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed while detecting silence: {TrimForError(result.Error)}");
        }

        var intervals = ParseSilenceIntervals(result.Error, duration);
        var leading = intervals.FirstOrDefault(interval => interval.Start <= TailDurationToleranceSeconds);
        var trailing = intervals.LastOrDefault(interval => Math.Abs(duration - interval.End) <= TailDurationToleranceSeconds);

        return (
            leading is null ? null : Math.Round(leading.End, 3),
            trailing is null ? null : Math.Round(duration - trailing.Start, 3));
    }

    private static List<SilenceInterval> ParseSilenceIntervals(string errorOutput, double duration)
    {
        var intervals = new List<SilenceInterval>();
        double? currentStart = null;

        foreach (var line in errorOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var startMatch = SilenceStartRegex().Match(line);
            if (startMatch.Success &&
                double.TryParse(startMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var start))
            {
                currentStart = start;
                continue;
            }

            var endMatch = SilenceEndRegex().Match(line);
            if (endMatch.Success &&
                currentStart is not null &&
                double.TryParse(endMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var end))
            {
                intervals.Add(new SilenceInterval(currentStart.Value, end));
                currentStart = null;
            }
        }

        if (currentStart is not null)
        {
            intervals.Add(new SilenceInterval(currentStart.Value, duration));
        }

        return intervals;
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(processTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) error.AppendLine(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {fileName}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"{fileName} exceeded the {processTimeout.TotalSeconds:0.#} second timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static double GetDouble(IConfiguration configuration, string key, double defaultValue)
    {
        var value = configuration[key];
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string TrimForError(string text)
    {
        const int maxLength = 500;
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup after cancellation or timeout.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temp file cleanup should not mask the analysis result.
        }
    }

    [GeneratedRegex(@"I:\s*(?<value>-?\d+(?:\.\d+)?)\s*LUFS", RegexOptions.CultureInvariant)]
    private static partial Regex IntegratedLufsRegex();

    [GeneratedRegex(@"silence_start:\s*(?<value>-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex SilenceStartRegex();

    [GeneratedRegex(@"silence_end:\s*(?<value>-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex SilenceEndRegex();

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed record SilenceInterval(double Start, double End);
}
