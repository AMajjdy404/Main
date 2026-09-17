using Main.Application;
using Main.Core.Models;
using Main.Core.Models.Settings;
using Main.Infrastructure.Data;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Main.API.Extensions
{
    public static class IdentityExtension
    {
        public static IServiceCollection AddIdentityService(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JWT").Get<JwtSettings>();
            if (string.IsNullOrWhiteSpace(jwtSettings?.Key))
                throw new InvalidOperationException("JWT:Key is missing from configuration.");

            services.AddOptions<JwtSettings>()
                .Bind(configuration.GetSection("JWT"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Hangfire
            services.AddHangfire(config =>
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                      .UseSimpleAssemblyNameTypeSerializer()
                      .UseRecommendedSerializerSettings()
                      .UseSqlServerStorage(
                          configuration.GetConnectionString("DefaultConnection"),
                          new SqlServerStorageOptions
                          {
                              CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                              SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                              QueuePollInterval = TimeSpan.Zero,
                              UseRecommendedIsolationLevel = true,
                              DisableGlobalLocks = true
                          }));

            services.AddHangfireServer();
            services.AddScoped<RefreshTokenCleanupJob>();

            // Logging
            services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            });

            // Identity
            services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;

                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // JWT Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.ValidIssuer,

                    ValidateAudience = true,
                    ValidAudience = jwtSettings.ValidAudience,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),

                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = async context =>
                    {
                        context.NoResult();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var message = context.Exception is SecurityTokenExpiredException
                            ? "Token expired. Please login again."
                            : "Invalid token.";

                        var result = JsonSerializer.Serialize(new
                        {
                            message
                        });

                        await context.Response.WriteAsync(result);
                    },

                    OnMessageReceived = context =>
                    {
                        var tokenFromHeader = context.Request.Headers["Authorization"]
                            .FirstOrDefault()?.Split(" ").Last();

                        if (!string.IsNullOrWhiteSpace(tokenFromHeader))
                        {
                            context.Token = tokenFromHeader;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            // Secure by default: any endpoint without an explicit [AllowAnonymous]
            // requires an authenticated user, even if a developer forgets [Authorize].
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            return services;
        }
    }
}
