using Main.Core.Models;
using Main.Core.Specifications;

namespace Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs
{
    public class ActiveRefreshTokensByOwnerSpecification : BaseSpecification<RefreshToken>
    {
        public ActiveRefreshTokensByOwnerSpecification(string ownerId, string userType)
        {
            Criteria = rt =>
                rt.OwnerId == ownerId &&
                rt.UserType == userType &&
                !rt.IsRevoked &&
                !rt.IsCompromised &&
                rt.ExpiresAt > DateTime.UtcNow;

            ApplyOrderBy(rt => rt.CreatedAt);
        }
    }
}
