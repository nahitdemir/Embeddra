using System.Reflection;
using System.Text;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Messaging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.DependencyInjection;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.Admin.WebApi.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Application.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-admin");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-admin", "logs-embeddra-admin", serviceVersion);

builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminUI", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        var configuredOrigins = builder.Configuration["Admin:CorsOrigins"];
        if (!string.IsNullOrWhiteSpace(configuredOrigins))
        {
            var origins = configuredOrigins
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddEmbeddraAdminPersistence(builder.Configuration);
builder.Services.AddEmbeddraAuditLogging(builder.Configuration);
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, AdminRequestResponseLoggingPolicy>();
builder.Services.AddEmbeddraRabbitMq(builder.Configuration);
builder.Services.AddSingleton<IIngestionJobPublisher, IngestionJobPublisher>();
var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(); // For Search API proxy requests
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAllElasticApm();

var app = builder.Build();

app.UseCors("AdminUI");
app.UseAuthentication();
app.UseMiddleware<BearerTokenGuardMiddleware>();
app.UseMiddleware<JwtTenantMiddleware>();
app.UseEmbeddraMiddleware();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
