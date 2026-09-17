namespace Main.Core.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public string OwnerId { get; set; } = string.Empty;

        public string UserType { get; set; } = string.Empty;

        public string? DeviceInfo { get; set; }

        public string? DeviceId { get; set; }

        public string? RemoteIpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        public DateTime? RevokedAt { get; set; }

        public string? RevokedByIpAddress { get; set; }

        public string? ReplacedByTokenHash { get; set; }

        public bool IsRememberMe { get; set; } = false;

        public bool IsCompromised { get; set; } = false;

        public DateTime? CompromisedAt { get; set; }

        public string? CompromisedReason { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        public bool IsActive => !IsRevoked && !IsExpired && !IsCompromised;
    }
}
