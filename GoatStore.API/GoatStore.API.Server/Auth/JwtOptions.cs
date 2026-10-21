using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GoatStore.API.Server.Auth
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Required, MinLength(32)]
        public string SigningKey { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int AccessTokenMinutes { get; set; } = 480;

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
    }
}
