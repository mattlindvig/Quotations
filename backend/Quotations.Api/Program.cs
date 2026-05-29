using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Quotations.Api.BackgroundServices;
using Quotations.Api.Services;
using Quotations.Api.Configuration;
using Quotations.Api.Data;
using Quotations.Api.Extensions;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;

// Configure MongoDB serialization conventions
var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
ConventionRegistry.Register("camelCase", conventionPack, t => true);

// Register class maps for embedded reference types
if (!BsonClassMap.IsClassMapRegistered(typeof(AuthorReference)))
{
    BsonClassMap.RegisterClassMap<AuthorReference>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
    });
}

if (!BsonClassMap.IsClassMapRegistered(typeof(SourceReference)))
{
    BsonClassMap.RegisterClassMap<SourceReference>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
    });
}

if (!BsonClassMap.IsClassMapRegistered(typeof(UserReference)))
{
    BsonClassMap.RegisterClassMap<UserReference>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
    });
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Quotations API",
        Version = "v1",
        Description = "API for managing and browsing quotations from various sources (books, movies, speeches, etc.)",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Quotations Development Team",
            Email = "dev@quotations.example.com"
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter your JWT token in the text input below."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<MongoDbService>().Database.Client,
        name: "mongodb",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "mongodb" });

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// App and email settings
builder.Services.AddOptions<Quotations.Api.Configuration.AppSettings>()
    .Bind(builder.Configuration.GetSection("AppSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<Quotations.Api.Configuration.ResendSettings>(
    builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();

// Register application services
builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
builder.Services.AddScoped<ISourceRepository, SourceRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAiReviewErrorRepository, AiReviewErrorRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IQuoteOfDayRepository, QuoteOfDayRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<QuotationService>();

// AI Review services
builder.Services.Configure<AiReviewOptions>(builder.Configuration.GetSection("AiReview"));
builder.Services.AddHttpClient<IAnthropicService, AnthropicService>();
builder.Services.AddScoped<AiReviewService>();
builder.Services.AddScoped<IAiReviewQueueService, AiReviewQueueService>();
builder.Services.AddScoped<IAiBatchJobRepository, AiBatchJobRepository>();

// Chat service
builder.Services.AddHttpClient<ChatService>();
builder.Services.AddSingleton(new AiReviewRuntimeSettings());
builder.Services.AddHostedService<AiReviewBackgroundService>();
builder.Services.AddHostedService<AiBatchProcessingService>();

// Configure JWT Authentication (using custom implementation, not ASP.NET Identity)
builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);

// Configure CORS — set AllowedOrigins env var in production (comma-separated)
var allowedOrigins = builder.Configuration["AllowedOrigins"]
    ?.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:5173", "https://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Trust X-Forwarded-For from Railway's reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting — keyed by IP address
builder.Services.AddRateLimiter(options =>
{
    // Auth endpoints: 10 requests/min (brute force protection)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });

    // Chat endpoint: 20 requests/min per IP (Anthropic cost protection)
    options.AddFixedWindowLimiter("chat", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 20;
        opt.QueueLimit = 0;
    });

    // Bulk submission: 5 requests/min per IP (queue flooding protection)
    options.AddFixedWindowLimiter("submissions", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global error handling middleware (must be first)
app.UseMiddleware<Quotations.Api.Middleware.ErrorHandlingMiddleware>();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// Resolve real client IP from X-Forwarded-For (must be before rate limiter)
app.UseForwardedHeaders();

// CORS middleware
app.UseCors("AllowFrontend");

app.UseRateLimiter();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health Check Endpoints
app.MapHealthChecks("/api/v1/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Simple liveness probe (no dependencies checked)
app.MapHealthChecks("/api/v1/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false  // Don't run any checks, just return healthy if app is running
});

// Readiness probe (checks all dependencies)
app.MapHealthChecks("/api/v1/health/ready");

// Seed database with sample data
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var mongoDbService = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    var database = mongoDbService.GetDatabase();

    try
    {
        // Create indexes
        await MongoIndexes.CreateIndexesAsync(database);

        // Seed sample data (dev only — avoids known credentials in production)
        if (app.Environment.IsDevelopment())
        {
            await DataSeeder.SeedDataAsync(database);
        }
    }
    catch (MongoDB.Driver.MongoAuthenticationException ex)
    {
        logger.LogCritical(ex, "MongoDB authentication failed. Check that MONGO_PASSWORD matches the password used to initialize the data volume. To reset: docker compose down -v && docker compose up");
        Environment.Exit(1);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to initialize database. The application cannot start.");
        Environment.Exit(1);
    }
}

app.Run();