using KwikOff.Web.Components;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Server components with increased message size
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API Controllers for REST endpoints
builder.Services.AddControllers();

// Configure SignalR hub options for VERY large payloads
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = null; // Unlimited
    options.MaximumParallelInvocationsPerClient = 10;
    options.StreamBufferCapacity = 10;
    options.EnableDetailedErrors = true;
});

// Configure JSON options for large exports - CRITICAL for large data
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.MaxDepth = 256;
    options.SerializerOptions.DefaultBufferSize = 1024 * 1024 * 10; // 10MB buffer
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.MaxDepth = 256;
    options.JsonSerializerOptions.DefaultBufferSize = 1024 * 1024 * 10; // 10MB buffer
});

// Configure Kestrel server limits for large responses
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 500; // 500MB
    options.Limits.MaxResponseBufferSize = 1024 * 1024 * 100; // 100MB response buffer
});

// Get connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add PostgreSQL with Entity Framework Core for regular request handling
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(120);
        }));

// Add DbContextFactory for background services
// Configure separately to avoid scoped options injection issue
builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(sp =>
{
    return new ManualDbContextFactory(connectionString);
});

// Add MediatR for CQRS pattern
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add application services
builder.Services.AddKwikOffServices();

// Add HttpClient for Open Food Facts API
builder.Services.AddHttpClient("OpenFoodFacts", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OpenFoodFacts:ApiBaseUrl"] ?? "https://world.openfoodfacts.org");
    client.DefaultRequestHeaders.Add("User-Agent", builder.Configuration["OpenFoodFacts:UserAgent"] ?? "KwikOff/1.0");
    client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large downloads
});

// Add HttpClient for KwikKart Integration
builder.Services.AddHttpClient("KwikKart", client =>
{
    var baseUrl = builder.Configuration["KwikKart:BaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    
    var apiKey = builder.Configuration["KwikKart:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }
    
    var timeoutSeconds = builder.Configuration.GetValue<int>("KwikKart:TimeoutSeconds", 300);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

// Configure KwikKart settings
builder.Services.Configure<KwikOff.Web.Infrastructure.Services.KwikKartSettings>(
    builder.Configuration.GetSection("KwikKart"));

var app = builder.Build();

// Reset any stuck syncs from previous crashes/restarts
_ = Task.Run(async () =>
{
    try
    {
        // Delay to let the app fully start and database connections settle
        await Task.Delay(TimeSpan.FromSeconds(3));
        
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stuckSyncs = await dbContext.SyncStatuses
            .Where(s => s.IsSyncing)
            .ToListAsync();
        
        if (stuckSyncs.Any())
        {
            foreach (var sync in stuckSyncs)
            {
                sync.IsSyncing = false;
                sync.StatusMessage = "Sync stopped - app restarted";
            }
            await dbContext.SaveChangesAsync();
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Reset {Count} stuck sync(s) from previous session", stuckSyncs.Count);
        }
    }
    catch
    {
        // Silently ignore - not critical for app startup
    }
});

// Skip migrations - database schema manually updated
// if (app.Environment.IsDevelopment())
// {
//     using var scope = app.Services.CreateScope();
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     await db.Database.MigrateAsync();
// }

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapControllers(); // Map API controllers
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Manual factory implementation to avoid DI issues
public class ManualDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly string _connectionString;
    
    public ManualDbContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(_connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(300);
            });
        return new AppDbContext(optionsBuilder.Options);
    }
}
