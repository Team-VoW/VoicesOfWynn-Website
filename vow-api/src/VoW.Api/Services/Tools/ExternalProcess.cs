using System.Diagnostics;
using System.Text;

namespace VoW.Api.Services.Tools;

public sealed record ExternalProcessResult(int ExitCode, string Output, string Error);

/// <summary>Runs ffmpeg / ffprobe style tools with bounded output capture and a hard timeout.</summary>
public static class ExternalProcess
{
    private const int OutputMaxCharacters = 64 * 1024;

    public static async Task<ExternalProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
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

        var output = new BoundedStringBuilder(OutputMaxCharacters);
        var error = new BoundedStringBuilder(OutputMaxCharacters);
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
            throw new TimeoutException($"{fileName} exceeded the {timeout.TotalSeconds:0.#} second timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }

        return new ExternalProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    public static string TrimForError(string text)
    {
        const int maxLength = 500;
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static void TryDelete(string path)
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
            // Temp file cleanup should not mask the actual result.
        }
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

    private sealed class BoundedStringBuilder(int maxCharacters)
    {
        private readonly StringBuilder builder = new();
        private readonly object gate = new();

        public void AppendLine(string value)
        {
            lock (gate)
            {
                builder.AppendLine(value);
                if (builder.Length > maxCharacters)
                {
                    builder.Remove(0, builder.Length - maxCharacters);
                }
            }
        }

        public override string ToString()
        {
            lock (gate)
            {
                return builder.ToString();
            }
        }
    }
}
