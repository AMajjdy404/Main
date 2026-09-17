using System.ComponentModel.DataAnnotations;

namespace Main.Core.Models.Settings
{
    public class JwtSettings
    {
        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string ValidIssuer { get; set; } = string.Empty;

        [Required]
        public string ValidAudience { get; set; } = string.Empty;

        [Range(1, double.MaxValue)]
        public double AccessTokenDurationInMinutes { get; set; }

        [Range(1, double.MaxValue)]
        public double RefreshTokenDurationInDays { get; set; }

        [Range(1, double.MaxValue)]
        public double RefreshTokenRememberMeDurationInDays { get; set; }
    }
}
