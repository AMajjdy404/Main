using Main.Application;
using Main.Core.Interfaces;
using Main.Core.Models.Settings;
using Main.Core.Services;
using Main.Infrastructure.ReposAndSpecs;

namespace Main.API.Extensions
{
    public static class ApplicationServiceExtension
    {
        public static IServiceCollection AddApplicationService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAppAuthService, AppAuthService>();
            services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
            services.AddHttpClient<ISmsService, SmsService>();

            services.Configure<SmsSettings>(configuration.GetSection("SmsSettings"));

            return services;
        }
    }
}
