using Main.Core.Interfaces;
using Main.Core.Models;
using Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Main.Application
{
    public class RefreshTokenCleanupJob
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<RefreshTokenCleanupJob> _logger;
        private readonly IConfiguration _configuration;

        public RefreshTokenCleanupJob(
            IUnitOfWork uow,
            ILogger<RefreshTokenCleanupJob> logger,
            IConfiguration configuration)
        {
            _uow = uow;
            _logger = logger;
            _configuration = configuration;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ExecuteAsync()
        {
            var retainExpiredDays = _configuration.GetValue<int>("RefreshTokenCleanup:RetainExpiredDays", 7);
            var retainRevokedDays = _configuration.GetValue<int>("RefreshTokenCleanup:RetainRevokedDays", 90);

            var repo = _uow.Repository<RefreshToken>();

            var expiredTokens = await repo.ListAsync(new ExpiredNonRevokedTokensSpecification(retainExpiredDays));
            var revokedTokens = await repo.ListAsync(new OldRevokedTokensSpecification(retainRevokedDays));

            if (expiredTokens.Any())
                repo.RemoveRange(expiredTokens);

            if (revokedTokens.Any())
                repo.RemoveRange(revokedTokens);

            if (expiredTokens.Any() || revokedTokens.Any())
                await _uow.SaveChangesAsync();
        }
    }
}
