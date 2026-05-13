using Azure.Storage.Blobs;

namespace VoW.Api.Services.Storage;

public static class StorageConfiguration
{
    private const string DefaultContainerName = "vow-dynamic";

    public static string GetContainerName(IConfiguration configuration) =>
        configuration["Storage:ContainerName"] ?? DefaultContainerName;

    public static string GetBaseUrl(IConfiguration configuration)
    {
        var configuredBaseUrl = configuration["Storage:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return NormalizeBaseUrl(configuredBaseUrl);
        }

        var blobServiceClient = new BlobServiceClient(GetConnectionString(configuration));
        return NormalizeBaseUrl(new Uri(blobServiceClient.Uri, $"{GetContainerName(configuration)}/").ToString());
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not configured.");

    private static string NormalizeBaseUrl(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}
