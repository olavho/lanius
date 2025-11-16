using Lanius.Api.Hubs;
using Lanius.Api.Services;
using Lanius.Business.Configuration;
using Lanius.Business.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

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

// Register background monitoring service
builder.Services.AddSingleton<RepositoryMonitoringService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RepositoryMonitoringService>());

// Configure CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // Adjust for your frontend
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

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<RepositoryHub>("/hubs/repository");

app.Run();
