using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data;
using SummaryCmp.Web.Middleware;
using SummaryCmp.Web.Services;
using SummaryCmp.Web.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<ProvidersOptions>(builder.Configuration.GetSection("Providers"));

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<ISummaryProvider, ClaudeProvider>();
builder.Services.AddScoped<ISummaryProvider, AzureOpenAIProvider>();
builder.Services.AddScoped<ISummaryProvider, GeminiProvider>();
builder.Services.AddScoped<ISummaryProvider, MicrosoftFoundryProvider>();
builder.Services.AddScoped<SummarizationService>();
builder.Services.AddScoped<LeaderboardService>();

builder.Services.AddHttpClient();
builder.Services.AddRazorPages();

var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SeedData.InitializeAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<SimpleAuthMiddleware>();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
