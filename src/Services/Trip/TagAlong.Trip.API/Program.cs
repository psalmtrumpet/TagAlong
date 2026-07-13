
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using TagAlong.Common.Behaviors;
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
builder.Services.AddScoped<IGoogleDirectionsClient>(sp => sp.GetRequiredService<GoogleDirectionsClient>());

builder.Services.AddScoped<ITripRouteService, TripRouteService>();
builder.Services.AddScoped<IDetourVerifier, DetourVerifier>();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<RouteEnrichmentService>();

builder.Services.AddScoped<ITripRepository, TripRepository>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateTripCommand>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672",
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

app.Run();
