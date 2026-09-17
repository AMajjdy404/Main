
using Main.API.Errors;
using Main.API.Extensions;
using Main.API.Middlewares;
using Main.Application;
using Main.Infrastructure.Data;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace Main.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Host.UseSerilog((ctx, lc) => lc
                                .MinimumLevel.Information()
                                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                                .WriteTo.Console()
                                .WriteTo.File(
                                 path: Path.Combine(ctx.HostingEnvironment.ContentRootPath, "logs", "app-.log"),
                                 rollingInterval: RollingInterval.Day,
                                 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                                 retainedFileCountLimit: 31)
                                .Enrich.FromLogContext()
                                  .Enrich.WithMachineName()
                                );

            // Add services to the container.
            builder.Services.AddControllers(options =>
            {
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var details = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .SelectMany(entry => entry.Value!.Errors.Select(error => error.ErrorMessage))
                        .ToList();

                    var response = new ApiErrorResponse(
                        StatusCodes.Status400BadRequest,
                        "بيانات غير صحيحة.",
                        string.Join(" | ", details));

                    return new BadRequestObjectResult(response);
                };
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Main API",
                    Version = "v1",
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT Token like this: Bearer {your token}"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                      new string[] {}
                   }
                 });
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            builder.Services.AddIdentityService(builder.Configuration);
            builder.Services.AddApplicationService(builder.Configuration);
            builder.Services.AddCustomCors(builder.Configuration);

            builder.Services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>();

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Brute-force mitigation for login: caps attempts per client IP,
                // independent from (and in addition to) the per-account Identity lockout.
                options.AddPolicy("login", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            });

            var app = builder.Build();

            // Structured request logging (method, path, status code, elapsed time)
            // for every request - separate from ExceptionMiddleware, which only logs failures.
            app.UseSerilogRequestLogging();

            // Global Exception Middleware
            app.UseMiddleware<ExceptionMiddleware>();


            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseCors("CorsPolicy");

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            // AllowAnonymous: health probes (load balancers, container orchestrators) can't
            // present a JWT, and the FallbackPolicy above would otherwise block them with a 401.
            app.MapHealthChecks("/health").AllowAnonymous();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                // Base template default: dashboard only reachable from the host machine.
                // Replace with a real IDashboardAuthorizationFilter (e.g. require an Admin role)
                // before exposing this behind a public domain.
                Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() }
            });

            app.MapControllers();

            app.Lifetime.ApplicationStarted.Register(() =>
            {
                using var scope = app.Services.CreateScope();
                var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

                var cronExpression = builder.Configuration.GetValue<string>("RefreshTokenCleanup:CronExpression");

                if (string.IsNullOrWhiteSpace(cronExpression))
                {
                    var intervalHours = builder.Configuration.GetValue<int?>("RefreshTokenCleanup:IntervalHours");
                    cronExpression = intervalHours is > 0 ? Cron.HourInterval(intervalHours.Value) : Cron.Daily();
                }

                recurringJobManager.AddOrUpdate<RefreshTokenCleanupJob>(
                    "refresh-token-cleanup",
                    job => job.ExecuteAsync(),
                    cronExpression);
            });

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

                try
                {
                    var dbContext = services.GetRequiredService<AppDbContext>();
                    await dbContext.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    // Fail fast: an app that "starts" with unapplied migrations looks healthy
                    // but returns 500s on the first real DB call. Better to crash loudly here.
                    logger.LogCritical(ex, "Database migration failed. Aborting startup.");
                    throw;
                }
            }

            app.Run();
        }
    }
}
