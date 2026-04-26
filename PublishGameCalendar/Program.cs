using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PublishGameCalendar.Data;
using PublishGameCalendar.Identity;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;
using PublishGameCalendar.Services.Orchestrator;
using PublishGameCalendar.Services.Pollers;
using PublishGameCalendar.Services.Queue;
using RabbitMQ.Client;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

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

// ── RabbitMQ ──
builder.Services.AddSingleton<IConnectionFactory>(_ =>
{
    IConfigurationSection rmq = builder.Configuration.GetSection("RabbitMq");
    return new ConnectionFactory
    {
        HostName = rmq["Host"] ?? "rabbitmq",
        Port = int.Parse(rmq["Port"] ?? "5672"),
        UserName = rmq["Username"] ?? "guest",
        Password = rmq["Password"] ?? "guest"
    };
});

// ── Repositories ──
builder.Services.AddScoped<ISeriesRepository, SeriesRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPollingConfigRepository, PollingConfigRepository>();

// ── Services ──
builder.Services.AddSingleton<IIcsService, IcsService>();
builder.Services.AddTransient<StubPoller>();
builder.Services.AddHttpClient<Poller1>();
builder.Services.AddTransient<IPollerFactory, PollerFactory>();
builder.Services.AddTransient<IQueueAdapter, RabbitMqQueueAdapter>();

// ── Orchestrator ──
builder.Services.AddHostedService<OrchestratorService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

// ── Migrate & seed roles ──
using (IServiceScope scope = app.Services.CreateScope())
{
    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    await db.Database.MigrateAsync();

    foreach (string role in new[] { Roles.Admin, Roles.User })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();