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

// Configure CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
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

// Map SignalR hub (will be added in Phase 4)
// app.MapHub<RepositoryHub>("/hubs/repository");

app.Run();
