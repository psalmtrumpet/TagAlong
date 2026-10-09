
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using TagAlong.Common.Behaviors;
using TagAlong.EventBus;
using TagAlong.Trip.API.IntegrationEvents;
using TagAlong.EventBus.RabbitMQ;
using TagAlong.Trip.API;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.Domain.Repositories;
using TagAlong.Trip.Infrastructure.Persistence;
using TagAlong.Trip.Infrastructure.Repositories;
using TagAlong.Trip.Infrastructure.Services;

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine(@"
  _____     _         _    ____ ___
 |_   _| __(_)_ __   / \  |  _ \_ _|
   | || '__| | '_ \ / _ \ | |_) | |
   | || |  | | |_) / ___ \|  __/| |
   |_||_|  |_| .__/_/   \_\_|  |___|
             |_|
");
Console.ResetColor();
Console.WriteLine("TagAlong Trip Service - Starting...\n");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TagAlong Trip API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TripDb"),
        x => x.UseNetTopologySuite()));

builder.Services.AddDbContextFactory<TripDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TripDb"),
        x => x.UseNetTopologySuite()), ServiceLifetime.Scoped);

builder.Services.AddHttpClient<GoogleDirectionsClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("GoogleMaps:DirectionsTimeoutSeconds", 5));
});
// Google first, free OSRM routing when Google fails (e.g. an expired key)
builder.Services.AddHttpClient<OsrmDirectionsClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TagAlong/1.0 (+https://tlimc.net)");
});
builder.Services.AddScoped<IGoogleDirectionsClient>(sp => new FallbackDirectionsClient(
    sp.GetRequiredService<OsrmDirectionsClient>(),
    sp.GetRequiredService<ILogger<FallbackDirectionsClient>>(),
    string.IsNullOrWhiteSpace(builder.Configuration["GoogleMaps:ApiKey"]) ? null : sp.GetRequiredService<GoogleDirectionsClient>()));

// Meet points on a driver's route (OSRM + OpenStreetMap bus stops)
builder.Services.AddHttpClient("osrm", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TagAlong/1.0 (+https://tlimc.net)");
});
builder.Services.AddHttpClient("overpass", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TagAlong/1.0 (+https://tlimc.net)");
});
builder.Services.AddScoped<MeetPointService>();
builder.Services.AddScoped<RouteOptionsService>();

builder.Services.AddScoped<ITripRouteService, TripRouteService>();
builder.Services.AddScoped<IDetourVerifier, DetourVerifier>();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<RouteEnrichmentService>();
builder.Services.AddHostedService<TripExpiryService>();
// user-api: checks a driver's licence/vehicle approval before they can post trips
builder.Services.AddHttpClient("user-api", c => c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:UserApi"] ?? "http://user-api/"));
builder.Services.AddScoped<TripBookingsChangedIntegrationEventHandler>();

builder.Services.AddScoped<ITripRepository, TripRepository>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateTripCommand>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? throw new InvalidOperationException("RabbitMQ connection string not configured"),
    "trip-service-queue");

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
    db.Database.Migrate();
}

// Subscribe to events
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<TripBookingsChangedIntegrationEvent, TripBookingsChangedIntegrationEventHandler>();

app.Run();
