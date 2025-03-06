using Azure.Core;
using Azure.Identity;
using ExchangeTest.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Graph;
using Microsoft.Identity.Client;

// using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.OpenApi.Models;
using MM.Common.Api.Utilities;
// using WebApplication = Microsoft.AspNetCore.Builder.WebApplication;

var builder = WebApplication.CreateBuilder(args);
// Logging
builder.ConfigureElkLogging();
NLog.LogManager.GetCurrentClassLogger().Info("Initializing...");

// Configuration
IConfigurationRoot configRoot = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();
builder.Configuration.AddEnvFile();

// Add services to the container.
// builder.ConfigureSwagger(configRoot);

// Configure Swagger to use OAuth2
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Select Health Medical Management - Exchange Test Api", Version = "v1" });

    // Define the OAuth2 scheme that's in use
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "https://graph.microsoft.com/Mail.Read", "Read user email" }
                }
            }
        }
    });

    // Apply the OAuth2 scheme globally
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "https://graph.microsoft.com/Mail.Read" }
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDistributedTokenCaches();
builder.Services.Configure<MsalDistributedTokenCacheAdapterOptions>(options =>
{
    options.Encrypt = true;
    options.SlidingExpiration = TimeSpan.FromDays(14);
    options.DisableL1Cache = false;
});
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TokenCache");
    options.SchemaName = configRoot.GetSection("TokenCache")["SchemaName"];
    options.TableName = configRoot.GetSection("TokenCache")["TableName"];
});


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
// app.ConfigureSwagger();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Select Health Medical Management - Exchange Test Api v1");
    c.RoutePrefix = "swagger";
    c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
    c.OAuthClientSecret(builder.Configuration["AzureAd:ClientSecret"]);
    // c.OAuthUsePkce(); // Use PKCE (Proof Key for Code Exchange)
    c.OAuth2RedirectUrl("http://localhost:5000"); // Specify your redirect URI here
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// app.MapGet("/", (HttpContext context, ILogger<Program> logger) =>
// {
//     logger.LogInformation("/{QueryString}", context.Request.QueryString);

//     context.Response.Redirect("/attachments" + context.Request.QueryString);
// });

app.MapGet("/", async (HttpContext context, IConfiguration configuration, ILogger<Program> logger) =>
{
    logger.LogInformation("Query String: {QueryString}", context.Request.QueryString);

    var tenantId = configuration.GetSection("AzureAd")["TenantId"];
    if (tenantId == null)
    {
        logger.LogError("AzureAd:TenantId is not configured");
        // return Results.InternalServerError("AzureAd:TenantId is not configured");
    }
    var clientId = configuration.GetSection("AzureAd")["ClientId"];
    if (clientId == null)
    {
        logger.LogError("AzureAd:ClientId is not configured");
        // return Results.InternalServerError("AzureAd:ClientId is not configured");
    }
    var clientSecret = configuration.GetSection("AzureAd")["ClientSecret"];
    if (clientSecret == null)
    {
        logger.LogError("AzureAd:ClientSecret is not configured");
        // return Results.InternalServerError("AzureAd:ClientSecret is not configured");
    }
    // use locally logged on user credential (az login)
    //   - user account must have tenant wide permissions to read email
    // var credential = new DefaultAzureCredential();
    
    // use client secret credential approach
    //   - client must have tenant wide permission to read email
    // var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    // use username password credential approach (specify user single email and password)
    //   - this doesn't work with federated accounts (PingID)
    // var email = configuration.GetSection("Exchange")["Email"];
    // var password = configuration.GetSection("Exchange")["Password"];

    // var credential = new UsernamePasswordCredential(email, password, tenantId, clientId);

    // use interactive browser credential approach
    //   - redirects browser to login.microsoftonline.com
    // if (string.IsNullOrEmpty(context.Request.QueryString.Value))
    // {
    //     new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
    //     {
    //         TenantId = tenantId,
    //         ClientId = clientId,
    //         RedirectUri = new Uri("http://localhost:5000")
    //     });
    //     return Results.Ok();
    // }

    var code = context.Request.Query["code"].FirstOrDefault();

    // use AuthorizationCodeCredential approach
    // var options = new AuthorizationCodeCredentialOptions
    // {
    //     RedirectUri = new Uri("http://localhost:5000")
    // };
    // var credential = new AuthorizationCodeCredential(tenantId, clientId, clientSecret, code, options);
    
    // request a token and log it
    //   - credential (i.e. authorization code) can only be used once
    //     once GetTokenAsync is call, credential cannot be use to create GraphServiceClient
 
    // ether this
    // var tokenRequestContext = new TokenRequestContext(["https://graph.microsoft.com/Mail.Read"]);
    // var token = await credential.GetTokenAsync(tokenRequestContext);
    // logger.LogInformation($"JWT Token: {token.Token}");
    // return Results.Ok(token.Token);

    // or this
    // var graphClient = new GraphServiceClient(credential);

    // use MSAL ConfidentialClientApplication approach
    //   - retrieving refresh token is not permitted due to security reasons
    //     https://stackoverflow.com/questions/61058614/how-to-get-refresh-token-in-msal-net-c-sharp
    var app = ConfidentialClientApplicationBuilder.Create(clientId)
        .WithClientSecret(clientSecret)
        .WithRedirectUri("http://localhost:5000")
        .WithAuthority(new Uri($"{configuration["AzureAd:Instance"]}{tenantId}"))
        .Build();

    var result = await app.AcquireTokenByAuthorizationCode(["https://graph.microsoft.com/Mail.Read"], code)
        .ExecuteAsync();

    // Log the access token and refresh token
    logger.LogInformation($"Access Token: {result.AccessToken}");
    // logger.LogInformation($"Refresh Token: {result.RefreshToken}");

    // use raw access token to create GraphServiceClient
    var graphClient = new GraphServiceClient(
        new BaseBearerTokenAuthenticationProvider(new TokenProvider { AccessToken = result.AccessToken }));

    var userInbox = graphClient.Me;

    var messagesResponse = await userInbox.Messages.GetAsync();
    if (messagesResponse == null || messagesResponse.Value == null)
    {
        return Results.NotFound();
    }

    var myMessage = messagesResponse.Value.FirstOrDefault(m => m.HasAttachments ?? false);
    if (myMessage == null) return Results.NotFound();

    var attachmentsResponse = await userInbox.Messages[myMessage.Id].Attachments.GetAsync();
    var myAttachment = attachmentsResponse?.Value?.FirstOrDefault();
    logger.LogInformation($"Attachment Count: {attachmentsResponse?.Value?.Count}, Attachment Name: {myAttachment?.Name}");

    var attachmentTasks = messagesResponse.Value?
        .Select(async m => (await userInbox.Messages[m.Id].Attachments.GetAsync())?.Value ?? []) ?? [];

    var attachments = (await Task.WhenAll(attachmentTasks))
        .SelectMany(a => a)
        .Select(a => new { myMessage.Subject, AttachmentId = a.Id, Filename = a.Name });

    return attachments.Any() ? Results.Ok(attachments) : Results.NotFound();
    // return Results.File(fileAttachment.ContentBytes, fileAttachment.ContentType, fileAttachment.Name);
})
.WithName("GetAttachments");

app.Run();

internal class TokenProvider : IAccessTokenProvider
{
    public string? AccessToken { get; set; }
    public AllowedHostsValidator AllowedHostsValidator => throw new NotImplementedException();

    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AccessToken ?? "");
    }
}