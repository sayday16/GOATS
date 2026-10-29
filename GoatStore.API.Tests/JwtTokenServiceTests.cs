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

        private static AuthResponseDto IssueTokenForOwner()
        {
            var response = SuccessResponse();
            CreateService().WithAccessToken(response);
            return response;
        }

        private static string ClaimValue(AuthResponseDto response, string type) =>
            Read(response).Claims.Single(c => c.Type == type).Value;

        private static AuthResponseDto IssueTokenForFailedResponse()
        {
            var response = new AuthResponseDto { IsSuccess = false, Error = AuthError.Unauthorized };
            CreateService().WithAccessToken(response);
            return response;
        }

        private static DateTime ExpectedExpiry => Now.UtcDateTime.AddMinutes(Options.AccessTokenMinutes);

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsUserIdClaim()
        {
            var response = IssueTokenForOwner();

            Assert.Equal(response.UserId.ToString(), ClaimValue(response, GoatClaims.UserId));
        }

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsEmailClaim()
        {
            var response = IssueTokenForOwner();

            Assert.Equal("owner@test.com", ClaimValue(response, GoatClaims.Email));
        }

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsRoleClaim()
        {
            var response = IssueTokenForOwner();

            Assert.Equal("Admin", ClaimValue(response, GoatClaims.Role));
        }

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsStoreIdClaim()
        {
            var response = IssueTokenForOwner();

            Assert.Equal(response.StoreId.ToString(), ClaimValue(response, GoatClaims.StoreId));
        }

        [Fact]
        public void WithAccessToken_SuccessfulResponse_AddsTokenId()
        {
            var response = IssueTokenForOwner();

            Assert.False(string.IsNullOrEmpty(ClaimValue(response, JwtRegisteredClaimNames.Jti)));
        }

        [Fact]
        public void WithAccessToken_ReturnsExpiryAfterConfiguredMinutes()
        {
            var response = IssueTokenForOwner();

            Assert.Equal(ExpectedExpiry, response.AccessTokenExpiresAt);
        }

        [Fact]
        public void WithAccessToken_TokenExpiresAfterConfiguredMinutes()
        {
            var response = IssueTokenForOwner();

            Assert.Equal(ExpectedExpiry, Read(response).ValidTo);
        }

        [Fact]
        public void WithAccessToken_Token_IsValidWithSameKey()
        {
            var response = IssueTokenForOwner();

            var exception = Record.Exception(() => new JwtSecurityTokenHandler()
                .ValidateToken(response.AccessToken, ValidationParameters(Options.CreateSigningKey()), out _));

            Assert.Null(exception);
        }

        [Fact]
        public void WithAccessToken_Token_IsRejectedWithOtherKey()
        {
            var response = IssueTokenForOwner();
            var otherKey = new SymmetricSecurityKey("another-signing-key-0123456789abcdef0123"u8.ToArray());

            Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler()
                .ValidateToken(response.AccessToken, ValidationParameters(otherKey), out _));
        }

        [Fact]
        public void WithAccessToken_FailedResponse_ReturnsWithoutToken()
        {
            var response = IssueTokenForFailedResponse();

            Assert.Null(response.AccessToken);
        }

        [Fact]
        public void WithAccessToken_FailedResponse_ReturnsWithoutExpiry()
        {
            var response = IssueTokenForFailedResponse();

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
