using Microsoft.ApplicationInsights.Extensibility;
using WAiSA.API.Security.Auditing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure audit logging
builder.Services.AddAuditLogging(builder.Configuration);

// Optional: Add Application Insights
if (builder.Configuration.GetValue<bool>("AuditLogging:EnableApplicationInsights"))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    });
}

// Alternative: Configure with custom options
builder.Services.AddAuditLogging(options =>
{
    options.EnableFileLogging = true;
    options.LogDirectory = "/var/log/agent-audit";
    options.EnableApplicationInsights = false;
    options.LogRetentionDays = 30;
    options.CompressedLogRetentionDays = 365;
    options.EnableCompression = true;
    options.IncludeStackTraces = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
