using Main.Core.Models;
using Main.Core.Specifications;

namespace Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs
{
    public class NonCompromisedRefreshTokensByOwnerSpecification : BaseSpecification<RefreshToken>
    {
        public NonCompromisedRefreshTokensByOwnerSpecification(string ownerId, string userType)
        {
            Criteria = rt =>
                rt.OwnerId == ownerId &&
                rt.UserType == userType &&
                !rt.IsCompromised &&
                rt.ExpiresAt > DateTime.UtcNow;
        }
    }
}
