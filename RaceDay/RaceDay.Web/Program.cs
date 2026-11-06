using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

// Numbers in forms are always sent in the standard format (21.1, not 21,1), even when Windows
// is set to a South African region where a comma is the decimal separator
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Pages are never cached, so pressing Back after logging out can't show a protected page again
    options.Filters.Add(new ResponseCacheAttribute { NoStore = true, Location = ResponseCacheLocation.None });
});

// Session: the website remembers who is logged in (and their API session cookie) here
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserSession>();

// The website never touches the database: it only talks to the API.
// The API address comes from configuration, so Docker can point it at the API container.
// UseCookies = false because we attach each user's own API cookie ourselves.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

var app = builder.Build();

// Unexpected problems show a friendly page in every environment. The details go to the log, not the screen.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Missing pages (404) and other HTTP errors get the same friendly page
app.UseStatusCodePagesWithReExecute("/Home/HttpError", "?code={0}");

// Containers only listen on plain HTTP, so HTTPS redirection is only used when running locally
if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();