using System.IdentityModel.Tokens.Jwt;
using GoatStore.API.Server.Auth;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GoatStore.API.Tests
{
    public class JwtTokenServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        private static readonly JwtOptions Options = new()
        {
            Issuer = "GoatStore.Tests",
            Audience = "GoatStore.Tests.Clients",
            SigningKey = "test-signing-key-0123456789abcdef0123456789",
            AccessTokenMinutes = 480
        };

        private static JwtTokenService CreateService() =>
            new(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(Now));

        private static AuthResponseDto SuccessResponse(Guid? storeId = null) => new()
        {
            IsSuccess = true,
            UserId = Guid.NewGuid(),
            Email = "owner@test.com",
            Role = UserRole.Admin,
            StoreId = storeId ?? Guid.NewGuid(),
            StoreName = "GoatShop"
        };

        private static JwtSecurityToken Read(AuthResponseDto response) =>
            new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        private static TokenValidationParameters ValidationParameters(SecurityKey key) => new()
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = key,
            ValidateLifetime = false
        };

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsTokenWithClaims()
        {
            var response = SuccessResponse();

            CreateService().WithAccessToken(response);

            var token = Read(response);
            Assert.Equal(response.UserId.ToString(), token.Claims.Single(c => c.Type == GoatClaims.UserId).Value);
            Assert.Equal("owner@test.com", token.Claims.Single(c => c.Type == GoatClaims.Email).Value);
            Assert.Equal("Admin", token.Claims.Single(c => c.Type == GoatClaims.Role).Value);
            Assert.Equal(response.StoreId.ToString(), token.Claims.Single(c => c.Type == GoatClaims.StoreId).Value);
            Assert.False(string.IsNullOrEmpty(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
        }

        [Fact]
        public void WithAccessToken_ExpiresAfterConfiguredMinutes()
        {
            var response = SuccessResponse();

            CreateService().WithAccessToken(response);

            var expected = Now.UtcDateTime.AddMinutes(Options.AccessTokenMinutes);
            Assert.Equal(expected, response.AccessTokenExpiresAt);
            Assert.Equal(expected, Read(response).ValidTo);
        }

        [Fact]
        public void WithAccessToken_Token_IsValidWithSameKeyAndRejectedWithOtherKey()
        {
            var response = SuccessResponse();
            CreateService().WithAccessToken(response);
            var handler = new JwtSecurityTokenHandler();

            handler.ValidateToken(response.AccessToken, ValidationParameters(Options.CreateSigningKey()), out _);

            var otherKey = new SymmetricSecurityKey("another-signing-key-0123456789abcdef0123"u8.ToArray());
            Assert.ThrowsAny<SecurityTokenException>(() =>
                handler.ValidateToken(response.AccessToken, ValidationParameters(otherKey), out _));
        }

        [Fact]
        public void WithAccessToken_FailedResponse_ReturnsWithoutToken()
        {
            var response = new AuthResponseDto { IsSuccess = false, Error = AuthError.Unauthorized };

            CreateService().WithAccessToken(response);

            Assert.Null(response.AccessToken);
            Assert.Null(response.AccessTokenExpiresAt);
        }

        [Fact]
        public void WithAccessToken_MissingUserId_ReturnsWithoutToken()
        {
            var response = SuccessResponse();
            response.UserId = null;

            CreateService().WithAccessToken(response);

            Assert.Null(response.AccessToken);
        }

        [Fact]
        public void WithAccessToken_MissingRole_ReturnsWithoutToken()
        {
            var response = SuccessResponse();
            response.Role = null;

            CreateService().WithAccessToken(response);

            Assert.Null(response.AccessToken);
        }

        [Fact]
        public void WithAccessToken_NoStore_OmitsStoreIdClaim()
        {
            var response = SuccessResponse();
            response.StoreId = null;

            CreateService().WithAccessToken(response);

            Assert.DoesNotContain(Read(response).Claims, c => c.Type == GoatClaims.StoreId);
        }

        [Fact]
        public void WithAccessToken_TwoCalls_HaveDifferentJti()
        {
            var first = SuccessResponse();
            var second = SuccessResponse();

            CreateService().WithAccessToken(first);
            CreateService().WithAccessToken(second);

            Assert.NotEqual(Read(first).Id, Read(second).Id);
        }
    }
}
