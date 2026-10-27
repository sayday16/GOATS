using System.ComponentModel.DataAnnotations;
using GoatStore.API.Server.Auth;

namespace GoatStore.API.Tests
{
    public class JwtOptionsTests
    {
        private const string ValidKey = "test-signing-key-0123456789abcdef";

        private static bool IsValid(JwtOptions options) =>
            Validator.TryValidateObject(options, new ValidationContext(options), new List<ValidationResult>(), validateAllProperties: true);

        [Fact]
        public void Validate_CorrectOptions_IsValid()
        {
            var options = new JwtOptions { Issuer = "issuer", Audience = "audience", SigningKey = ValidKey };

            Assert.True(IsValid(options));
        }

        [Theory]
        [InlineData("issuer", "audience", "too-short-key", 480)]
        [InlineData("issuer", "audience", "", 480)]
        [InlineData("", "audience", ValidKey, 480)]
        [InlineData("issuer", "", ValidKey, 480)]
        [InlineData("issuer", "audience", ValidKey, 0)]
        [InlineData("issuer", "audience", ValidKey, 1441)]
        public void Validate_InvalidOptions_Fails(string issuer, string audience, string signingKey, int minutes)
        {
            var options = new JwtOptions
            {
                Issuer = issuer,
                Audience = audience,
                SigningKey = signingKey,
                AccessTokenMinutes = minutes
            };

            Assert.False(IsValid(options));
        }
    }
}
