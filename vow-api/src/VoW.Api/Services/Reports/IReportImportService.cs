namespace VoW.Api.Services.Reports;

public interface IReportImportService
{
    /// <summary>
    /// Reads a sounds.json document and marks every line that has an associated audio file as voiced.
    /// </summary>
    Task<ReportImportServiceResult> ImportVoicedLinesAsync(
        Stream soundsJson,
        CancellationToken cancellationToken);
}
