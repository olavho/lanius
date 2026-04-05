using Lanius.Api.Hubs;
using Lanius.Api.Services;
using Lanius.Business.Configuration;
using Lanius.Business.Layout.Services;
using Lanius.Business.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Configure OpenAPI
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure options
builder.Services.Configure<RepositoryStorageOptions>(
    builder.Configuration.GetSection(RepositoryStorageOptions.SectionName));
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection(MonitoringOptions.SectionName));

// Register business services
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<ICommitAnalyzer, CommitAnalyzer>();
builder.Services.AddScoped<IBranchAnalyzer, BranchAnalyzer>();

// Register both layout engines
builder.Services.AddScoped<LogicalLayoutEngine>();
builder.Services.AddScoped<CalendarLayoutEngine>();

// Register layout engine factory
builder.Services.AddScoped<ILayoutEngine>(provider =>
{
    // Default to LogicalLayoutEngine for DI
    // Controller will manually resolve the correct engine based on mode
    return provider.GetRequiredService<LogicalLayoutEngine>();
});

// ReplayService is singleton but uses IServiceProvider to create scopes for ICommitAnalyzer
builder.Services.AddSingleton<IReplayService, ReplayService>();

// Register background monitoring service
builder.Services.AddSingleton<RepositoryMonitoringService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RepositoryMonitoringService>());

builder.Services.AddSingleton<ReplaySignalRBridge>();

// Configure CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:5000", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files from Lanius.Web wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<RepositoryHub>("/hubs/repository");

app.Run();
