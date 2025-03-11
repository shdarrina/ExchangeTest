// using Azure.Core;
// using Azure.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ExchangeTest.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
// using Microsoft.Identity.Client;

// using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.OpenApi.Models;
using MM.Common.Api.Utilities;

var builder = WebApplication.CreateBuilder(args);
// Logging
builder.ConfigureElkLogging();
var nlogger = NLog.LogManager.GetCurrentClassLogger();
nlogger.Info("Initializing...");

nlogger.Info($"Current Directory: {Environment.CurrentDirectory}");

// Configuration
var configRoot = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddEnvFile()
    .Build();

var instance = configRoot["AzureAd:Instance"];
if (instance == null)
{
    nlogger.Error("AzureAd:Instance is not configured");
    throw new Exception("AzureAd:Instance is not configured");
}
var tenantId = configRoot["AzureAd:TenantId"];
if (tenantId == null)
{
    nlogger.Error("AzureAd:TenantId is not configured");
    throw new Exception("AzureAd:TenantId is not configured");
}
var clientId = configRoot["AzureAd:ClientId"];
if (clientId == null)
{
    nlogger.Error("AzureAd:ClientId is not configured");
    throw new Exception("AzureAd:ClientId is not configured");
}
var clientSecret = configRoot["AzureAd:ClientSecret"];
if (clientSecret == null)
{
    nlogger.Error("AzureAd:ClientSecret is not configured");
    throw new Exception("AzureAd:ClientSecret is not configured");
}
var redirectUri = configRoot["AzureAd:RedirectUri"];
if (redirectUri == null)
{
    nlogger.Error("AzureAd:RedirectUri is not configured");
    throw new Exception("AzureAd:RedirectUri is not configured");
}
var scope = configRoot["AzureAd:Scope"];
if (scope == null)
{
    nlogger.Error("AzureAd:Scope is not configured");
    throw new Exception("AzureAd:Scope is not configured");
}
// var email = configRoot.GetSection("Exchange")["Email"];
// if (email == null)
// {
//     nlogger.Error("Exchange:Email is not configured");
//     throw new Exception("Exchange:Email is not configured");
// }
// var password = configRoot.GetSection("Exchange")["Password"];
// if (scope == null)
// {
//     nlogger.Error("Exchange:Password is not configured");
//     throw new Exception("Exchange:Password is not configured");
// }

// Add services to the container.
builder.Services.AddCors();

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
                AuthorizationUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { scope, "Read user email" }
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
            new[] { scope }
        }
    });
});

// builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDistributedTokenCaches();

// distributed token cache
builder.Services.Configure<MsalDistributedTokenCacheAdapterOptions>(options =>
{
    options.Encrypt = true;
    options.SlidingExpiration = TimeSpan.FromDays(14);
    options.DisableL1Cache = false;
});
// 1. use SQL Server distributed token cache
// builder.Services.AddDistributedSqlServerCache(options =>
// {
//     options.ConnectionString = builder.Configuration.GetConnectionString("TokenCache");
//     options.SchemaName = configRoot.GetSection("TokenCache")["SchemaName"];
//     options.TableName = configRoot.GetSection("TokenCache")["TableName"];
// });
// 2. use in memory distributed token cache
builder.Services.AddDistributedMemoryCache();
// 3. use simple memory cache
builder.Services.AddMemoryCache();
// 4. Simple scoped variable cache
// var accessToken = "";
// var refreshToken = "";

builder.Services.AddEndpointsApiExplorer();

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
    c.OAuth2RedirectUrl(builder.Configuration["AzureAd:RedirectUri"]); // Specify your redirect URI here
});
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
// app.MapControllers();

app.MapGet("/api/token", ([FromServices]ILogger<Program> logger) =>
{
    // 1. use InteractiveBrowserCredential helper class
    //   - redirects browser to login.microsoftonline.com
    // var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
    // {
    //     TenantId = tenantId,
    //     ClientId = clientId,
    //     RedirectUri = new Uri(redirectUri)
    // });
    // return Results.Ok();

    // 2. redirect using raw OAuth2 authorization code flow URL
    var url = $"{instance}/{tenantId}/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={redirectUri}&response_mode=query&scope={scope}&state=12345";
    logger.LogShInfo($"Redirecting to: {url}");
    return Results.Redirect(url);
})
.WithName("GetToken")
.WithDescription("Redirect to OAuth2 authorization code flow URL")
.WithDisplayName("Get Token")
.WithTags("Token");

app.MapGet("/", async (HttpContext context, [FromServices]IMemoryCache memoryCache, [FromServices]ILogger<Program> logger) =>
{
    logger.LogShInfo($"Query string: {context.Request.QueryString}");

    var code = context.Request.Query["code"].FirstOrDefault();
    if (string.IsNullOrEmpty(code))
    {
        logger.LogShError("Authorization code is missing");
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    // 1. use AuthorizationCodeCredential approach
    // var options = new AuthorizationCodeCredentialOptions
    // {
    //     RedirectUri = new Uri(redirectUri)
    // };
    // var credential = new AuthorizationCodeCredential(tenantId, clientId, clientSecret, code, options);
    
    //   - either request a token and log it.  credential (i.e. authorization code) can only be used once
    //     once GetTokenAsync is call, credential cannot be use to create GraphServiceClient
    // var tokenRequestContext = new TokenRequestContext([scope]);
    // var token = await credential.GetTokenAsync(tokenRequestContext);
    // logger.LogShInfo($"JWT Token: {token.Token}");
    // return Results.Ok(token.Token);

    //   - or save access token to in memory cache
    // memoryCache.Set("access_token", result.AccessToken, TimeSpan.FromMinutes(60));

    // 2. use MSAL ConfidentialClientApplication approach
    //   - retrieving refresh token is not permitted due to security reasons
    //     https://stackoverflow.com/questions/61058614/how-to-get-refresh-token-in-msal-net-c-sharp
    // var app = ConfidentialClientApplicationBuilder.Create(clientId)
    //     .WithClientSecret(clientSecret)
    //     .WithRedirectUri(redirectUri)
    //     .WithAuthority(new Uri($"{instance}{tenantId}"))
    //     .Build();

    // var result = await app.AcquireTokenByAuthorizationCode([scope], code)
    //     .ExecuteAsync();

    // log the access token. refresh token is not exposed
    // logger.LogShInfo($"Access Token: {result.AccessToken}");

    // save access token to in memory cache
    // memoryCache.Set("access_token", result.AccessToken, TimeSpan.FromMinutes(60));

    // 3. use HttpClient to call raw OAuth2 token URL to retrieve access a refresh token
    //   - this is not recommended due to security reasons
    var httpClient = new HttpClient();
    var tokenResponse = await httpClient.PostAsync(
        $"{instance}/{tenantId}/oauth2/v2.0/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "scope", scope }
        }));
    var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
    logger.LogShInfo($"Token Response: {tokenContent}");

    //  - parse the access token from the response
    var token = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenContent) ?? [];

    // - log access and refresh tokens from the response
    logger.LogShInfo($"Access Token: {token["access_token"]}");
    logger.LogShInfo($"Refresh Token: {token["refresh_token"]}");

    // parse unique_name from access_token
    var accessToken = $"{token["access_token"]}";
    var jwt = accessToken?.Split('.');
    var jwtPayload = jwt?.Length > 1 ? JsonSerializer.Deserialize<Dictionary<string, object>>(Base64UrlEncoder.Decode(jwt[1])) : default;
    var uniqueName = $"{jwtPayload?["unique_name"]}".ToLower();

    logger.LogShInfo($"Unique Name: {uniqueName}");

    // save access and refresh tokens to in memory cache
    memoryCache.Set($"{uniqueName}_access_token", $"{token["access_token"]}", TimeSpan.FromMinutes(60));
    memoryCache.Set($"{uniqueName}_refresh_token",$"{token["refresh_token"]}", TimeSpan.FromMinutes(60));

    // save access and refresh tokens to scoped variables
    // accessToken = $"{token["access_token"]}";
    // refreshToken = $"{token["refresh_token"]}";

    return Results.Ok(new
    {
        AccessToken = token["access_token"],
        RefreshToken = token["refresh_token"]
    });
})
.WithName("SaveToken")
.WithDescription("Receives the authorization token, requests and stores the access and refresh tokens to cache")
.WithDisplayName("Save Token")
.WithTags("Token");

app.MapGet("/api/attachments", async (HttpContext context, [FromQuery][Required]string email, [FromServices]IMemoryCache memoryCache, [FromServices]ILogger<Program> logger) =>
{
    email = email.ToLower();

    // 1. use locally logged on user credential (az login)
    //   - user account must have tenant wide permissions to read email
    // var credential = new DefaultAzureCredential();
    
    // 2. use client secret credential approach
    //   - client must have tenant wide permission to read email
    // var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    // 3. use username password credential approach (specify user single email and password)
    //   - this doesn't work with federated accounts (PingID)
    // var credential = new UsernamePasswordCredential(email, password, tenantId, clientId);
    // var graphClient = new GraphServiceClient(credential);

    // 4. retrieve access token from in memory cache
    //   - list all items in memory cache
    logger.LogShInfo($"Cache Items: {memoryCache.GetCurrentStatistics()?.CurrentEntryCount}");

    // - retrieve access token from memory cache
    var accessToken = memoryCache.Get($"{email}_access_token") as string;

    // - use access token from scoped variable: accessToken
    // accessToken = accessToken

    if (string.IsNullOrEmpty(accessToken))
    {
        var errorMessage = $"Access token is missing for email: {email}";
        logger.LogShError(errorMessage);
        return Results.NotFound(errorMessage);
    }
    logger.LogShInfo($"Access Token: {accessToken}");

    var graphClient = new GraphServiceClient(
        new BaseBearerTokenAuthenticationProvider(new TokenProvider { AccessToken = accessToken }));
    
    // use graph client to read email
    var userInbox = graphClient.Me;

    var messagesResponse = await userInbox.Messages.GetAsync(config =>
    {
        config.QueryParameters.Top = 1;
        // config.QueryParameters.Orderby = [ "receivedDateTime asc" ];
        config.QueryParameters.Select = [ "id", "subject" ];
        config.QueryParameters.Filter = "hasAttachments eq true";
    });
    if (messagesResponse == null || messagesResponse.Value == null)
    {
        var errorMessage = $"No emails with attachments found in inbox: {email}";
        logger.LogShError(errorMessage);
        return Results.NotFound(errorMessage);
    }

    var firstMessage = messagesResponse.Value.FirstOrDefault();
    if (firstMessage == null)
    {
        var errorMessage = $"Error retrieving oldest email from inbox: {email}";
        logger.LogShError(errorMessage);
        return Results.NotFound(errorMessage);
    }

    var emlStream = await userInbox.Messages[firstMessage.Id].Content.GetAsync();
    if (emlStream == null)
    {
        var errorMessage = $"Error retrieving email content from inbox: {email}";
        logger.LogShError(errorMessage);
        return Results.NotFound(errorMessage);
    }

    var emlContent = await new StreamReader(emlStream).ReadToEndAsync();

    // return .eml content with content disposition so the browser prompts user to download the file
    var contentDisposition = new System.Net.Mime.ContentDisposition
    {
        FileName = $"{firstMessage.Subject}.eml",
        Inline = false
    };
    context.Response.Headers.Append("Content-Disposition", contentDisposition.ToString());

    return Results.Content(emlContent, "message/rfc822");
})
.WithName("GetAttachments")
.WithDescription("Read email ttachments")
.WithDisplayName("Get Attachments")
.WithTags("Email");

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