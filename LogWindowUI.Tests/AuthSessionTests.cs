using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    [Collection(AuthSessionCollection.Name)]
    public class AuthSessionTests : AuthSessionTestBase
    {
        private static AuthResponseDto Response(string? token = "token", DateTime? expiresAt = null) => new()
        {
            IsSuccess = true,
            Email = "owner@test.com",
            Role = UserRole.Admin,
            AccessToken = token,
            AccessTokenExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(8)
        };

        [Fact]
        public void Start_SavesUserTokenAndExpiry()
        {
            var response = Response();

            AuthSession.Start(response);

            Assert.Same(response, AuthSession.User);
            Assert.Equal("token", AuthSession.AccessToken);
            Assert.Equal(response.AccessTokenExpiresAt, AuthSession.ExpiresAt);
            Assert.True(AuthSession.IsActive);
        }

        [Fact]
        public void Clear_RemovesSession()
        {
            AuthSession.Start(Response());

            AuthSession.Clear();

            Assert.Null(AuthSession.User);
            Assert.Null(AuthSession.AccessToken);
            Assert.Null(AuthSession.ExpiresAt);
            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public void IsActive_NoSession_ReturnsFalse()
        {
            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public void IsActive_ExpiredToken_ReturnsFalse()
        {
            AuthSession.Start(Response(expiresAt: DateTime.UtcNow.AddSeconds(-1)));

            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public void IsActive_WithoutToken_ReturnsFalse()
        {
            AuthSession.Start(Response(token: null));

            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public void IsActive_WithoutExpiry_ReturnsFalse()
        {
            var response = Response();
            response.AccessTokenExpiresAt = null;

            AuthSession.Start(response);

            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public void Start_SecondTime_ReplacesPreviousSession()
        {
            AuthSession.Start(Response(token: "old"));

            AuthSession.Start(Response(token: "new"));

            Assert.Equal("new", AuthSession.AccessToken);
        }
    }
}
