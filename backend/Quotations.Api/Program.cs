using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Quotations.Api.Data;
using Quotations.Api.Extensions;
using Quotations.Api.Models;
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
        mongodbConnectionString: builder.Configuration["MongoDbSettings:ConnectionString"] ?? "mongodb://localhost:27017",
        name: "mongodb",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "mongodb" });

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();

// Register application services
builder.Services.AddScoped<Quotations.Api.Repositories.IQuotationRepository, Quotations.Api.Repositories.QuotationRepository>();
builder.Services.AddScoped<Quotations.Api.Repositories.IAuthorRepository, Quotations.Api.Repositories.AuthorRepository>();
builder.Services.AddScoped<Quotations.Api.Repositories.ISourceRepository, Quotations.Api.Repositories.SourceRepository>();
builder.Services.AddScoped<QuotationService>();

// Configure JWT Authentication (using custom implementation, not ASP.NET Identity)
builder.Services.AddJwtAuthentication(builder.Configuration);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Global error handling middleware (must be first)
app.UseMiddleware<Quotations.Api.Middleware.ErrorHandlingMiddleware>();

// CORS middleware
app.UseCors("AllowFrontend");

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseMiddleware<Quotations.Api.Middleware.JwtMiddleware>();
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
    var mongoDbService = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    var database = mongoDbService.GetDatabase();

    // Create indexes
    await MongoIndexes.CreateIndexesAsync(database);

    // Seed sample data
    await DataSeeder.SeedDataAsync(database);
}

app.Run();