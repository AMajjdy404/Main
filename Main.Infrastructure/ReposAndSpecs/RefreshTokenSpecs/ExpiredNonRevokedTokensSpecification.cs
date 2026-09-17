using Main.Core.Models;
using Main.Core.Specifications;

namespace Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs
{
    public class ExpiredNonRevokedTokensSpecification : BaseSpecification<RefreshToken>
    {
        public ExpiredNonRevokedTokensSpecification(int retainDays = 7)
        {
            var threshold = DateTime.UtcNow.AddDays(-retainDays);

            Criteria = rt =>
                rt.ExpiresAt < threshold &&
                !rt.IsRevoked;
        }
    }
}
