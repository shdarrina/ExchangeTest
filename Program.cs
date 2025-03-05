using Azure.Identity;
using ExchangeTest.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Graph;
// using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
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
builder.ConfigureSwagger(configRoot);
builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
app.ConfigureSwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api", (HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation("/{QueryString}", context.Request.QueryString);

    context.Response.Redirect("/attachments" + context.Request.QueryString);
});

app.MapGet("/api/attachments", async (HttpContext context, IConfiguration configuration, ILogger<Program> logger) =>
{
    logger.LogInformation("/attachments{QueryString}", context.Request.QueryString);

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
    // var email = configuration.GetSection("Exchange")["Email"];
    // var password = configuration.GetSection("Exchange")["Password"];

    // var credential = new DefaultAzureCredential();
    // var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    // var credential = new UsernamePasswordCredential(email, password, tenantId, clientId);

    if (string.IsNullOrEmpty(context.Request.QueryString.Value))
    {
        new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            TenantId = tenantId,
            ClientId = clientId,
            RedirectUri = new Uri("http://localhost:5000")
        });
        return Results.Ok();
    }

    var code = context.Request.Query["code"].FirstOrDefault();
    var credential = new AuthorizationCodeCredential(tenantId, clientId, clientSecret, code);
    var graphClient = new GraphServiceClient(credential);

    var userInbox = graphClient.Me;

    var messagesResponse = await userInbox.Messages.GetAsync();
    if (messagesResponse == null || messagesResponse.Value == null)
    {
        return Results.NotFound();
    }

    var myMessage = messagesResponse.Value.FirstOrDefault(m => m.Subject?.Contains("Check out this cool photo") ?? false);
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