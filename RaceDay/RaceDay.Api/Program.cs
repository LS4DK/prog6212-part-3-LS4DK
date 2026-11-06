using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database: EF Core talking to SQL Server via the connection string in appsettings.json
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<PasswordService>();

// Image uploads go to Azure Blob Storage (tests swap this for a fake)
builder.Services.AddSingleton<IImageStorage, AzureBlobImageStorage>();

// Server-side session: the server remembers UserId + Role, the browser only holds a session cookie
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;     // JavaScript can't read the cookie
    options.Cookie.IsEssential = true;
});

// Enums travel as readable names ("Run", "Confirmed") instead of numbers.
// Two settings: one for the controllers' JSON, one so the Swagger documentation shows the same thing.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI document that Swagger UI displays. Summaries, parameters and responses come from the /// comments.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "RaceDay API",
            Version = "v1",
            Description = "REST API for the RaceDay event management platform (Organisers and Participants). " +
                          "To try protected endpoints: call POST /api/auth/login first. The session cookie is then " +
                          "sent automatically with every request made from this page. " +
                          "401 = not logged in, 403 = logged in with the wrong role or not the owner."
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "RaceDay API v1"));
}

// Containers only listen on plain HTTP, so HTTPS redirection is only used when running locally
if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    app.UseHttpsRedirection();

app.UseSession();          // must come before the controllers so they can read the session
app.UseAuthorization();
app.MapControllers();

// Docker: create or update the database when the container starts
if (app.Configuration.GetValue<bool>("RUN_MIGRATIONS"))
    await ApplyMigrationsAsync(app);

app.Run();

// SQL Server can take a little while to start, so keep trying for up to 90 seconds
static async Task ApplyMigrationsAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database is up to date.");
            return;
        }
        catch (Exception ex) when (attempt < 30)
        {
            logger.LogWarning("Database not ready yet (attempt {Attempt} of 30): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Lets the unit test project see the Program class
public partial class Program { }
