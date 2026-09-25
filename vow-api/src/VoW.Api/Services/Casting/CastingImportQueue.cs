using System.Threading.Channels;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Casting;

public sealed record CccImportJob(int RoundId, string ProjectUrl);

public interface ICastingImportQueue
{
    bool TryEnqueue(CccImportJob job);
}

/// <summary>
/// Imports run one at a time in the background: a large CCC project downloads and converts hundreds of
/// clips, far longer than an HTTP request should live. Progress is written to the round's import columns.
/// </summary>
public sealed class CastingImportQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<CastingImportQueue> logger) : BackgroundService, ICastingImportQueue
{
    private readonly Channel<CccImportJob> jobs = Channel.CreateBounded<CccImportJob>(
        new BoundedChannelOptions(20) { FullMode = BoundedChannelFullMode.Wait });

    public bool TryEnqueue(CccImportJob job) => jobs.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICastingRoundRepository>()
                .FailRunningImportsAsync(stoppingToken);
        }

        await foreach (var job in jobs.Reader.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<CccImportService>();
            try
            {
                await importer.RunAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await MarkInterruptedAsync(scope.ServiceProvider, job);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CCC import for casting round {RoundId} crashed.", job.RoundId);
                await MarkInterruptedAsync(scope.ServiceProvider, job, ex.Message);
            }
        }
    }

    private static async Task MarkInterruptedAsync(IServiceProvider services, CccImportJob job, string? reason = null)
    {
        try
        {
            await services.GetRequiredService<ICastingRoundRepository>().SetImportStateAsync(
                job.RoundId,
                CastingImportStatus.Failed,
                reason is null ? "The import was interrupted by an API restart. Start it again to continue." : $"Import failed: {reason}",
                CancellationToken.None);
        }
        catch
        {
            // Nothing more to do if the database is gone too.
        }
    }
}
