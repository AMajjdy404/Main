using Main.Core.Models;
using Main.Core.Specifications;

namespace Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs
{
    public class ActiveRefreshTokensExcludingDeviceSpecification : BaseSpecification<RefreshToken>
    {
        public ActiveRefreshTokensExcludingDeviceSpecification(string ownerId, string userType, string currentDeviceId)
        {
            Criteria = rt =>
                rt.OwnerId == ownerId &&
                rt.UserType == userType &&
                rt.DeviceId != currentDeviceId &&
                !rt.IsRevoked &&
                !rt.IsCompromised &&
                rt.ExpiresAt > DateTime.UtcNow;
        }
    }
}
