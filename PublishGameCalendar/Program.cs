using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Enrichment;
using PublishGameCalendar.Services.Ics;
using PublishGameCalendar.Services.Orchestrator;
using PublishGameCalendar.Services.Pollers;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── DynamoDB ──
builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
{
    AmazonDynamoDBConfig config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
            builder.Configuration["AWS:Region"] ?? "eu-west-1")
    };
    return new AmazonDynamoDBClient(config);
});
builder.Services.AddSingleton<DynamoDBContext>(sp =>
    new DynamoDBContext(sp.GetRequiredService<IAmazonDynamoDB>()));
builder.Services.AddSingleton<IDynamoDbContext>(sp =>
    new DynamoDbContextAdapter(sp.GetRequiredService<DynamoDBContext>()));

// ── JWT ──
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        IConfigurationSection jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            // ReSharper disable once NullableWarningSuppressionIsUsed
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });

builder.Services.AddAuthorization();

// ── Repositories ──
builder.Services.AddScoped<IPollingConfigRepository, DynamoDbPollingConfigRepository>();
builder.Services.AddScoped<ISeriesRepository>(sp =>
    new DynamoDbSeriesRepository(
        sp.GetRequiredService<IDynamoDbContext>(),
        sp.GetRequiredService<IPollingConfigRepository>()));
builder.Services.AddScoped<IEnrichmentRepository, DynamoDbEnrichmentRepository>();

// ── Services ──
builder.Services.AddSingleton<IIcsService, IcsService>();
builder.Services.AddTransient<StubPoller>();
builder.Services.AddHttpClient<Poller1>();
builder.Services.AddTransient<IPollerFactory, PollerFactory>();
builder.Services.AddTransient<IEventEnricher, EventEnricher>();

// ── Orchestrator ──
builder.Services.AddHostedService<OrchestratorService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
