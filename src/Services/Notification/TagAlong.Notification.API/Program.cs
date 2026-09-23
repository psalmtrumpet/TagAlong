
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using TagAlong.EventBus;
using TagAlong.EventBus.RabbitMQ;
using TagAlong.Notification.API.Hubs;
using TagAlong.Notification.API.IntegrationEvents;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Repositories;
using TagAlong.Notification.Infrastructure.Persistence;
using TagAlong.Notification.Infrastructure.Repositories;

Console.ForegroundColor = ConsoleColor.DarkCyan;
Console.WriteLine(@"
  _   _       _   _  __ _           _   _
 | \ | | ___ | |_(_)/ _(_) ___ __ _| |_(_) ___  _ __
 |  \| |/ _ \| __| | |_| |/ __/ _` | __| |/ _ \| '_ \
 | |\  | (_) | |_| |  _| | (_| (_| | |_| | (_) | | | |
 |_| \_|\___/ \__|_|_| |_|\___\__,_|\__|_|\___/|_| |_|
");
Console.ResetColor();
Console.WriteLine("TagAlong Notification Service - Starting...\n");

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TagAlong Notification API", Version = "v1" });
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

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

// Repositories
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IUserConnectionRepository, UserConnectionRepository>();
builder.Services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();

// Firebase (service account JSON stored in env var Firebase__ServiceAccountJson — NOT in git)
var firebaseJson = builder.Configuration["Firebase:ServiceAccountJson"];
if (!string.IsNullOrEmpty(firebaseJson))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromJson(firebaseJson)
    });
}
else
{
    Log.Warning("Firebase:ServiceAccountJson not configured — FCM push notifications disabled");
}

// Services
builder.Services.AddScoped<INotificationService, NotificationService>();

// SignalR with Redis backplane for scaling
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = "TagAlong";
        });
}
else
{
    builder.Services.AddSignalR();
}

// RabbitMQ
builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? throw new InvalidOperationException("RabbitMQ connection string not configured"),
    "notification-service-queue");

// Integration event handlers
builder.Services.AddScoped<DeliveryMatchedIntegrationEventHandler>();
builder.Services.AddScoped<DeliveryStatusChangedIntegrationEventHandler>();
builder.Services.AddScoped<PaymentCompletedIntegrationEventHandler>();
builder.Services.AddScoped<NegotiationMessageSentIntegrationEventHandler>();
builder.Services.AddScoped<ConversationRequestCreatedIntegrationEventHandler>();
builder.Services.AddScoped<KycStatusChangedIntegrationEventHandler>();

// JWT Authentication
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

    // Configure JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS for SignalR — explicit allowlist, no wildcard with credentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithOrigins(
                  "https://www.tlimc.net",
                  "https://tlimc.net",
                  "https://tagalong.delivery");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseCors("SignalRPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

// Subscribe to events
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<DeliveryMatchedIntegrationEvent, DeliveryMatchedIntegrationEventHandler>();
eventBus.Subscribe<DeliveryStatusChangedIntegrationEvent, DeliveryStatusChangedIntegrationEventHandler>();
eventBus.Subscribe<PaymentCompletedIntegrationEvent, PaymentCompletedIntegrationEventHandler>();
eventBus.Subscribe<NegotiationMessageSentIntegrationEvent, NegotiationMessageSentIntegrationEventHandler>();
eventBus.Subscribe<ConversationRequestCreatedIntegrationEvent, ConversationRequestCreatedIntegrationEventHandler>();
eventBus.Subscribe<KycStatusChangedIntegrationEvent, KycStatusChangedIntegrationEventHandler>();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();
}

app.Run();
