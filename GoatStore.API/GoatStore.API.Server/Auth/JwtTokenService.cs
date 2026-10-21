using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GoatStore.Core.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GoatStore.API.Server.Auth
{
    public static class GoatClaims
    {
        public const string UserId = JwtRegisteredClaimNames.Sub;
        public const string Email = "email";
        public const string Role = "role";
        public const string StoreId = "store_id";
    }

    public class JwtTokenService
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _time;

        public JwtTokenService(IOptions<JwtOptions> options, TimeProvider time)
        {
            _options = options.Value;
            _time = time;
        }

        public AuthResponseDto WithAccessToken(AuthResponseDto response)
        {
            if (!response.IsSuccess || response.UserId is null || response.Role is null)
                return response;

            var now = _time.GetUtcNow().UtcDateTime;
            var expires = now.AddMinutes(_options.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(GoatClaims.UserId, response.UserId.Value.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(GoatClaims.Email, response.Email ?? string.Empty),
                new(GoatClaims.Role, response.Role.Value.ToString())
            };

            if (response.StoreId is { } storeId)
                claims.Add(new Claim(GoatClaims.StoreId, storeId.ToString()));

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256));

            response.AccessToken = new JwtSecurityTokenHandler().WriteToken(token);
            response.AccessTokenExpiresAt = expires;
            return response;
        }
    }
}
