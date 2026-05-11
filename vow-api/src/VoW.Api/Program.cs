using System.Text;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using VoW.Api.Domain.Auth;
using VoW.Api.Repositories;
using VoW.Api.Services;
using VoW.Api.Services.Accounts;
using VoW.Api.Services.Analytics;
using VoW.Api.Services.Auth;
using VoW.Api.Services.Content;
using VoW.Api.Services.DiscordIntegration;
using VoW.Api.Services.Reports;
using VoW.Api.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IExternalAuthProvider, DiscordAuthService>();

builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IAuthHandoffService, AuthHandoffService>();
builder.Services.AddScoped<IUserAccessService, UserAccessService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IDiscordIntegrationService, DiscordIntegrationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IContentRepository, ContentRepository>();
builder.Services.AddScoped<IDiscordIntegrationRepository, DiscordIntegrationRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()["AZURE_STORAGE_CONNECTION_STRING"];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not configured.");
    }

    return new BlobServiceClient(connectionString);
});
builder.Services.AddSingleton<IQuestScriptStorage, AzureQuestScriptStorage>();
builder.Services.AddSingleton<INpcImageStorage, AzureNpcImageStorage>();
builder.Services.AddSingleton<IAccountAvatarStorage, AzureAccountAvatarStorage>();
builder.Services.AddSingleton<INpcRecordingStorage, AzureNpcRecordingStorage>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origin = builder.Configuration["CORS_ORIGIN"] ?? "https://app.voicesofwynn.com";
        policy.WithOrigins(origin)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type");
    });
});

var jwtSecret = JwtService.GetJwtSecret(builder.Configuration, builder.Environment);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtService.CreateTokenValidationParameters(signingKey);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenType = context.Principal?.FindFirst("type")?.Value;
                if (tokenType != JwtService.AccessTokenType)
                {
                    context.Fail("Access token required.");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var capability in CapabilityMapper.GetAllCapabilities())
    {
        var claimValue = CapabilityMapper.ToClaimValue(capability);
        options.AddPolicy(claimValue, policy =>
            policy.RequireClaim(CapabilityMapper.ClaimType, claimValue));
    }
});

var app = builder.Build();

var pathBase = builder.Configuration["PATH_BASE"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
