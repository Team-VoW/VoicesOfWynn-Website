using MySqlConnector;

namespace VoW.Api.Repositories;

public static class DatabaseSettings
{
    public static string GetApiConnectionString(IConfiguration configuration) =>
        BuildConnectionString(
            configuration["API_DB_HOST"],
            configuration["API_DB_NAME"],
            configuration["DB_USER"],
            configuration["DB_PASSWORD"],
            "api");

    public static string GetWebsiteConnectionString(IConfiguration configuration) =>
        BuildConnectionString(
            configuration["WEBSITE_DB_HOST"],
            configuration["WEBSITE_DB_NAME"],
            configuration["DB_USER"],
            configuration["DB_PASSWORD"],
            "website");

    private static string BuildConnectionString(string? host, string? database, string? user, string? password, string defaultDatabase)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = string.IsNullOrWhiteSpace(host) ? "localhost" : host,
            Database = string.IsNullOrWhiteSpace(database) ? defaultDatabase : database,
            UserID = string.IsNullOrWhiteSpace(user) ? "root" : user,
            Password = password ?? string.Empty,
            SslMode = MySqlSslMode.Preferred,
            AllowUserVariables = true
        };

        return builder.ConnectionString;
    }
}
