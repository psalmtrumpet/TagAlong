
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
using TagAlong.Package.API.Commands;
using TagAlong.Package.Domain.Repositories;
using TagAlong.Package.Infrastructure.Persistence;
using TagAlong.Package.Infrastructure.Repositories;

Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine(@"
  ____            _                        _    ____ ___
 |  _ \ __ _  ___| | ____ _  __ _  ___    / \  |  _ \_ _|
 | |_) / _` |/ __| |/ / _` |/ _` |/ _ \  / _ \ | |_) | |
 |  __/ (_| | (__|   < (_| | (_| |  __/ / ___ \|  __/| |
 |_|   \__,_|\___|_|\_\__,_|\__, |\___| /_/   \_\_|  |___|
                           |___/
");
Console.ResetColor();
Console.WriteLine("TagAlong Package Service - Starting...\n");

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TagAlong Package API", Version = "v1" });
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

builder.Services.AddDbContext<PackageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PackageDb")));

builder.Services.AddScoped<IPackageRequestRepository, PackageRequestRepository>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreatePackageRequestCommand>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672",
    "package-service-queue");

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
    var db = scope.ServiceProvider.GetRequiredService<PackageDbContext>();
    db.Database.Migrate();
}

app.Run();
