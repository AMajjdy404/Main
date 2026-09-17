using Main.Core.Models;
using Main.Core.Specifications;

namespace Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs
{
    public class OldRevokedTokensSpecification : BaseSpecification<RefreshToken>
    {
        public OldRevokedTokensSpecification(int retainDays = 90)
        {
            var threshold = DateTime.UtcNow.AddDays(-retainDays);

            Criteria = rt =>
                rt.IsRevoked &&
                rt.RevokedAt.HasValue &&
                rt.RevokedAt.Value < threshold;
        }
    }
}
