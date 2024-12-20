using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Graph.Models;
using WebApplication = Microsoft.AspNetCore.Builder.WebApplication;
using ExchangeTest.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddEnvFile();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowAllOrigins");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.MapGet("/attachments", async (IConfiguration configuration, ILogger<Program> logger) =>
{
    var tenantId = configuration.GetSection("AzureAd")["TenantId"];
    if (tenantId == null)
    {
        logger.LogError("AzureAd:TenantId is not configured");
        return Results.InternalServerError("AzureAd:TenantId is not configured");
    }
    var clientId = configuration.GetSection("AzureAd")["ClientId"];
    if (clientId == null)
    {
        logger.LogError("AzureAd:ClientId is not configured");
        return Results.InternalServerError("AzureAd:ClientId is not configured");
    }
    var clientSecret = configuration.GetSection("AzureAd")["ClientSecret"];
    if (clientSecret == null)
    {
        logger.LogError("AzureAd:ClientSecret is not configured");
        return Results.InternalServerError("AzureAd:ClientSecret is not configured");
    }

    // var credential = new DefaultAzureCredential();
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var graphClient = new GraphServiceClient(credential);

    var userInbox = graphClient.Users["darrin@sluggoworks.com"];

    var messagesResponse = await userInbox.Messages.GetAsync();
    if (messagesResponse == null || messagesResponse.Value == null)
    {
        return Results.NotFound();
    }

    var myMessage = messagesResponse.Value.FirstOrDefault(m => m.Subject?.Contains("Check out this cool photo") ?? false);
    if (myMessage != null)
    {
        var attachmentsResponse = await userInbox.Messages[myMessage.Id].Attachments.GetAsync();
        var myAttachment = attachmentsResponse?.Value?.FirstOrDefault();
        logger.LogInformation($"Attachment Count: {attachmentsResponse?.Value?.Count}, Attachment Name: {myAttachment?.Name}");
    }

    var attachmentTasks = messagesResponse.Value?
        .Select(async m => (await userInbox.Messages[m.Id].Attachments.GetAsync())?.Value ?? new List<Attachment>());
    var attachments = (await Task.WhenAll(attachmentTasks ?? []))
        .SelectMany(a => a)
        .Select(a => new { a.Id, a.Name });

    return (attachments?.Any() ?? false) ? Results.Ok(attachments) : Results.NotFound();
    // return Results.File(fileAttachment.ContentBytes, fileAttachment.ContentType, fileAttachment.Name);
})
.WithName("GetAttachments");

app.Run();