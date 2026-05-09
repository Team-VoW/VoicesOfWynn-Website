namespace VoW.Api.Services.Storage;

public interface IQuestScriptStorage
{
    Task UploadScriptAsync(string degeneratedName, Stream content, CancellationToken cancellationToken);

    Task<bool> ScriptExistsAsync(string degeneratedName, CancellationToken cancellationToken);

    Uri GetScriptUrl(string degeneratedName);
}
