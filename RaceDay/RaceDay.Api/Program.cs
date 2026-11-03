using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database: EF Core talking to SQL Server via the connection string in appsettings.json
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<PasswordService>();

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

app.UseHttpsRedirection();
app.UseSession();          // must come before the controllers so they can read the session
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the unit test project (Step 8) see the Program class
public partial class Program { }