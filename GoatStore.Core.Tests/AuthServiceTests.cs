using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Tests
{
    public sealed class AuthServiceTests : IDisposable
    {
        private const string StoreCode = "storecode123";

        private readonly TestDb _db = new();

        public void Dispose() => _db.Dispose();

        private AuthService CreateService() => new(_db.CreateContext());

        private static RegisterRequestDto AdminRequest(string storeName = "GoatShop", string email = "owner@test.com") => new()
        {
            Email = email,
            Password = "password123",
            ConfirmPassword = "password123",
            Role = UserRole.Admin,
            StoreName = storeName,
            StorePassword = StoreCode
        };

        private static RegisterRequestDto SellerRequest(string storeName = "GoatShop", string storePassword = StoreCode) => new()
        {
            Email = "seller@test.com",
            Password = "password123",
            ConfirmPassword = "password123",
            Role = UserRole.Seller,
            StoreName = storeName,
            StorePassword = storePassword
        };

        [Fact]
        public async Task RegisterAsync_AdminNewStore_CreatesStoreAndUser()
        {
            var result = await CreateService().RegisterAsync(AdminRequest());

            Assert.True(result.IsSuccess);
            Assert.Equal(AuthError.None, result.Error);
            Assert.Equal("GoatShop", result.StoreName);
            Assert.NotNull(result.StoreId);

            using var context = _db.CreateContext();
            var store = Assert.Single(context.Stores);
            var user = Assert.Single(context.Users);
            Assert.Equal(UserRole.Admin, user.Role);
            Assert.Equal(store.Id, user.StoreId);
            Assert.Equal(store.Id, result.StoreId);
        }

        [Theory]
        [InlineData("GoatShop")]
        [InlineData(" goatshop ")]
        [InlineData("GOATSHOP")]
        public async Task RegisterAsync_AdminExistingStoreName_ReturnsConflict(string storeName)
        {
            _db.AddStore("GoatShop");

            var result = await CreateService().RegisterAsync(AdminRequest(storeName));

            Assert.Equal(AuthError.Conflict, result.Error);
            using var context = _db.CreateContext();
            Assert.Single(context.Stores);
            Assert.Empty(context.Users);
        }

        [Fact]
        public async Task RegisterAsync_AdminShortStorePassword_ReturnsValidation()
        {
            var request = AdminRequest();
            request.StorePassword = "1234567";

            var result = await CreateService().RegisterAsync(request);

            Assert.Equal(AuthError.Validation, result.Error);
            using var context = _db.CreateContext();
            Assert.Empty(context.Stores);
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_JoinsStore()
        {
            var store = _db.AddStore("GoatShop", StoreCode);

            var result = await CreateService().RegisterAsync(SellerRequest());

            Assert.True(result.IsSuccess);
            Assert.Equal(store.Id, result.StoreId);
            using var context = _db.CreateContext();
            Assert.Single(context.Stores);
            var user = Assert.Single(context.Users);
            Assert.Equal(UserRole.Seller, user.Role);
            Assert.Equal(store.Id, user.StoreId);
        }

        [Fact]
        public async Task RegisterAsync_SellerWrongStorePassword_ReturnsForbidden()
        {
            _db.AddStore("GoatShop", StoreCode);

            var result = await CreateService().RegisterAsync(SellerRequest(storePassword: "wrongcode123"));

            Assert.Equal(AuthError.Forbidden, result.Error);
            using var context = _db.CreateContext();
            Assert.Empty(context.Users);
        }

        [Fact]
        public async Task RegisterAsync_SellerUnknownStore_ReturnsValidation()
        {
            var result = await CreateService().RegisterAsync(SellerRequest("Unknown Store"));

            Assert.Equal(AuthError.Validation, result.Error);
            Assert.StartsWith("Магазин не знайдено", result.Message);
        }

        public static TheoryData<string> InvalidEmails => new()
        {
            "",
            "not-an-email",
            new string('a', 250) + "@test.com"
        };

        [Theory]
        [MemberData(nameof(InvalidEmails))]
        public async Task RegisterAsync_InvalidEmail_ReturnsValidation(string email)
        {
            var result = await CreateService().RegisterAsync(AdminRequest(email: email));

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(129)]
        public async Task RegisterAsync_PasswordLengthOutOfRange_ReturnsValidation(int length)
        {
            var request = AdminRequest();
            request.Password = request.ConfirmPassword = new string('p', length);

            var result = await CreateService().RegisterAsync(request);

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_PasswordsDoNotMatch_ReturnsValidation()
        {
            var request = AdminRequest();
            request.ConfirmPassword = "different123";

            var result = await CreateService().RegisterAsync(request);

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Theory]
        [InlineData("", StoreCode)]
        [InlineData("   ", StoreCode)]
        [InlineData("GoatShop", "")]
        [InlineData("GoatShop", "   ")]
        public async Task RegisterAsync_MissingStoreData_ReturnsValidation(string storeName, string storePassword)
        {
            var request = AdminRequest(storeName);
            request.StorePassword = storePassword;

            var result = await CreateService().RegisterAsync(request);

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_TooLongStoreName_ReturnsValidation()
        {
            var result = await CreateService().RegisterAsync(AdminRequest(new string('s', 101)));

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData((UserRole)99)]
        public async Task RegisterAsync_InvalidRole_ReturnsValidation(UserRole? role)
        {
            var request = AdminRequest();
            request.Role = role;

            var result = await CreateService().RegisterAsync(request);

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmailDifferentCase_ReturnsConflict()
        {
            await CreateService().RegisterAsync(AdminRequest("Store A", "owner@gmail.com"));

            var result = await CreateService().RegisterAsync(AdminRequest("Store B", "Owner@Gmail.com"));

            Assert.Equal(AuthError.Conflict, result.Error);
            using var context = _db.CreateContext();
            Assert.Single(context.Users);
            Assert.Single(context.Stores);
        }

        [Fact]
        public async Task RegisterAsync_Email_StoredLowercaseAndTrimmed()
        {
            var result = await CreateService().RegisterAsync(AdminRequest(email: "  Owner@Gmail.COM "));

            Assert.Equal("owner@gmail.com", result.Email);
            using var context = _db.CreateContext();
            Assert.Equal("owner@gmail.com", context.Users.Single().Email);
        }

        [Fact]
        public async Task RegisterAsync_Passwords_StoredAsBCryptHash()
        {
            await CreateService().RegisterAsync(AdminRequest());

            using var context = _db.CreateContext();
            var user = context.Users.Single();
            var store = context.Stores.Single();
            Assert.NotEqual("password123", user.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("password123", user.PasswordHash));
            Assert.NotEqual(StoreCode, store.StorePasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(StoreCode, store.StorePasswordHash));
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsUserWithStore()
        {
            var store = _db.AddStore("GoatShop");
            var user = _db.AddUser(store.Id, "owner@test.com", "password123", UserRole.Admin);

            var result = await CreateService().LoginAsync(new LoginRequestDto { Email = "owner@test.com", Password = "password123" });

            Assert.True(result.IsSuccess);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal("owner@test.com", result.Email);
            Assert.Equal(UserRole.Admin, result.Role);
            Assert.Equal(store.Id, result.StoreId);
            Assert.Equal("GoatShop", result.StoreName);
        }

        [Fact]
        public async Task LoginAsync_EmailWithSpacesAndUppercase_Succeeds()
        {
            var store = _db.AddStore();
            _db.AddUser(store.Id, "owner@test.com", "password123");

            var result = await CreateService().LoginAsync(new LoginRequestDto { Email = "  Owner@TEST.com ", Password = "password123" });

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
        {
            var store = _db.AddStore();
            _db.AddUser(store.Id, "owner@test.com", "password123");

            var result = await CreateService().LoginAsync(new LoginRequestDto { Email = "owner@test.com", Password = "wrongpass123" });

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.Unauthorized, result.Error);
            Assert.Null(result.UserId);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ReturnsSameResponseAsWrongPassword()
        {
            var store = _db.AddStore();
            _db.AddUser(store.Id, "owner@test.com", "password123");

            var wrongPassword = await CreateService().LoginAsync(new LoginRequestDto { Email = "owner@test.com", Password = "wrongpass123" });
            var unknownEmail = await CreateService().LoginAsync(new LoginRequestDto { Email = "nobody@test.com", Password = "wrongpass123" });

            Assert.Equal(wrongPassword.Error, unknownEmail.Error);
            Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        }

        [Theory]
        [InlineData("", "password123")]
        [InlineData("   ", "password123")]
        [InlineData("owner@test.com", "")]
        public async Task LoginAsync_EmptyEmailOrPassword_ReturnsValidation(string email, string password)
        {
            var result = await CreateService().LoginAsync(new LoginRequestDto { Email = email, Password = password });

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_EmailTakenByParallelRequest_ReturnsConflict()
        {
            var store = _db.AddStore("GoatShop", StoreCode);
            var interceptor = new BeforeSaveInterceptor(() => _db.AddUser(store.Id, "seller@test.com"));

            var result = await new AuthService(_db.CreateContext(interceptor)).RegisterAsync(SellerRequest());

            Assert.Equal(AuthError.Conflict, result.Error);
            Assert.Equal("Користувач із таким Email вже існує.", result.Message);
            using var context = _db.CreateContext();
            Assert.Single(context.Users);
        }

        [Fact]
        public async Task RegisterAsync_StoreNameTakenByParallelRequest_ReturnsConflict()
        {
            var interceptor = new BeforeSaveInterceptor(() => _db.AddStore("GoatShop"));

            var result = await new AuthService(_db.CreateContext(interceptor)).RegisterAsync(AdminRequest());

            Assert.Equal(AuthError.Conflict, result.Error);
            Assert.StartsWith("Магазин із такою назвою вже існує", result.Message);
            using var context = _db.CreateContext();
            Assert.Single(context.Stores);
            Assert.Empty(context.Users);
        }

        [Fact]
        public async Task RegisterAsync_OtherDbError_IsRethrown()
        {
            var interceptor = new BeforeSaveInterceptor(() => throw new DbUpdateException("disk full"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => new AuthService(_db.CreateContext(interceptor)).RegisterAsync(AdminRequest()));
        }

        [Fact]
        public async Task GetUserAsync_ExistingUser_ReturnsUserWithStore()
        {
            var store = _db.AddStore("GoatShop");
            var user = _db.AddUser(store.Id, "seller@test.com");

            var result = await CreateService().GetUserAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("seller@test.com", result.Email);
            Assert.Equal(UserRole.Seller, result.Role);
            Assert.Equal("GoatShop", result.StoreName);
        }

        [Fact]
        public async Task GetUserAsync_UnknownId_ReturnsUnauthorized()
        {
            var result = await CreateService().GetUserAsync(Guid.NewGuid());

            Assert.Equal(AuthError.Unauthorized, result.Error);
            Assert.StartsWith("Сесія недійсна", result.Message);
        }
    }
}
