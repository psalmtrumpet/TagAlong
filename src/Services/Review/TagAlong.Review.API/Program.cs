
using System.Reflection;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TagAlong.EventBus;
using TagAlong.EventBus.RabbitMQ;
using TagAlong.Review.Domain.Repositories;
using TagAlong.Review.Infrastructure.Persistence;
using TagAlong.Review.Infrastructure.Repositories;

Console.ForegroundColor = ConsoleColor.DarkGreen;
Console.WriteLine(@"
  ____            _                _    ____ ___
 |  _ \ _____   _(_) _____      __/ \  |  _ \_ _|
 | |_) / _ \ \ / / |/ _ \ \ /\ / / _ \ | |_) | |
 |  _ <  __/\ V /| |  __/\ V  V / ___ \|  __/| |
 |_| \_\___| \_/ |_|\___| \_/\_/_/   \_\_|  |___|
");
Console.ResetColor();
Console.WriteLine("TagAlong Review Service - Starting...\n");

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ReviewDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add repositories
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// Add RabbitMQ Event Bus
var rabbitMQSettings = builder.Configuration.GetSection("RabbitMQ");
var rabbitMQHost = rabbitMQSettings["HostName"] ?? "localhost";
var rabbitMQUser = rabbitMQSettings["UserName"] ?? "guest";
var rabbitMQPass = rabbitMQSettings["Password"] ?? "guest";
builder.Services.AddRabbitMQEventBus(
    $"amqp://{rabbitMQUser}:{rabbitMQPass}@{rabbitMQHost}:5672",
    "review_queue");

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TagAlong Review API", Version = "v1" });
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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
