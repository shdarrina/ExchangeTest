using Microsoft.Graph;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

app.MapGet("/attachments", async (/*IConfiguration configuration,*/ ILogger<Program> logger) =>
{
    // var tenantId = configuration.GetSection("AzureAd")["TenantId"];
    // if (tenantId == null)
    // {
    //     logger.LogError("AzureAd:TenantId is not configured");
    //     return Results.InternalServerError("AzureAd:TenantId is not configured");
    // }
    // var clientId = configuration.GetSection("AzureAd")["ClientId"];
    // if (clientId == null)
    // {
    //     logger.LogError("AzureAd:ClientId is not configured");
    //     return Results.InternalServerError("AzureAd:ClientId is not configured");
    // }
    // var clientSecret = configuration.GetSection("AzureAd")["ClientSecret"];
    // if (clientSecret == null)
    // {
    //     logger.LogError("AzureAd:ClientSecret is not configured");
    //     return Results.InternalServerError("AzureAd:ClientSecret is not configured");
    // }
    var credential = new DefaultAzureCredential();
    var graphClient = new GraphServiceClient(credential);
    var messages = (await graphClient.Me.Messages.GetAsync())?.Value ?? [];

    foreach (var message in messages)
    {
        var attachments = (await graphClient.Me.Messages[message.Id].Attachments.GetAsync())?.Value ?? [];

        return Results.Ok(attachments.Select(a => new { a.Id, a.Name }));
        // return Results.File(fileAttachment.ContentBytes, fileAttachment.ContentType, fileAttachment.Name);
    }

    return Results.NotFound();
})
.WithName("GetAttachments");

app.Run();