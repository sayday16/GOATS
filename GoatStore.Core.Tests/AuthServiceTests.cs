using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
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

        private TResult Query<TResult>(Func<GoatStoreDbContext, TResult> query)
        {
            using var context = _db.CreateContext();
            return query(context);
        }

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

        private Task<AuthResponseDto> RegisterAdminAsync() => CreateService().RegisterAsync(AdminRequest());

        private async Task<(Store Store, AuthResponseDto Result)> RegisterSellerIntoExistingStoreAsync()
        {
            var store = _db.AddStore("GoatShop", StoreCode);
            var result = await CreateService().RegisterAsync(SellerRequest());
            return (store, result);
        }

        private async Task<AuthResponseDto> RegisterAdminWithTakenStoreNameAsync(string storeName)
        {
            _db.AddStore("GoatShop");
            return await CreateService().RegisterAsync(AdminRequest(storeName));
        }

        private Task<AuthResponseDto> RegisterAdminWithShortStorePasswordAsync()
        {
            var request = AdminRequest();
            request.StorePassword = "1234567";
            return CreateService().RegisterAsync(request);
        }

        private Task<AuthResponseDto> RegisterSellerWithWrongStorePasswordAsync()
        {
            _db.AddStore("GoatShop", StoreCode);
            return CreateService().RegisterAsync(SellerRequest(storePassword: "wrongcode123"));
        }

        private async Task<AuthResponseDto> RegisterDuplicateEmailInOtherCaseAsync()
        {
            await CreateService().RegisterAsync(AdminRequest("Store A", "owner@gmail.com"));
            return await CreateService().RegisterAsync(AdminRequest("Store B", "Owner@Gmail.com"));
        }

        private async Task<(Store Store, User User, AuthResponseDto Result)> LoginAsOwnerAsync()
        {
            var store = _db.AddStore("GoatShop");
            var user = _db.AddUser(store.Id, "owner@test.com", "password123", UserRole.Admin);
            var result = await CreateService().LoginAsync(new LoginRequestDto { Email = "owner@test.com", Password = "password123" });
            return (store, user, result);
        }

        private Task<AuthResponseDto> LoginWithWrongPasswordAsync()
        {
            var store = _db.AddStore();
            _db.AddUser(store.Id, "owner@test.com", "password123");
            return CreateService().LoginAsync(new LoginRequestDto { Email = "owner@test.com", Password = "wrongpass123" });
        }

        private async Task<(AuthResponseDto WrongPassword, AuthResponseDto UnknownEmail)> LoginWithWrongPasswordAndUnknownEmailAsync()
        {
            var wrongPassword = await LoginWithWrongPasswordAsync();
            var unknownEmail = await CreateService().LoginAsync(new LoginRequestDto { Email = "nobody@test.com", Password = "wrongpass123" });
            return (wrongPassword, unknownEmail);
        }

        private Task<AuthResponseDto> RegisterSellerWhileParallelRequestTakesEmailAsync()
        {
            var store = _db.AddStore("GoatShop", StoreCode);
            var interceptor = new BeforeSaveInterceptor(() => _db.AddUser(store.Id, "seller@test.com"));
            return new AuthService(_db.CreateContext(interceptor)).RegisterAsync(SellerRequest());
        }

        private Task<AuthResponseDto> RegisterAdminWhileParallelRequestTakesStoreNameAsync()
        {
            var interceptor = new BeforeSaveInterceptor(() => _db.AddStore("GoatShop"));
            return new AuthService(_db.CreateContext(interceptor)).RegisterAsync(AdminRequest());
        }

        private Task<AuthResponseDto> GetExistingSellerAsync()
        {
            var store = _db.AddStore("GoatShop");
            var user = _db.AddUser(store.Id, "seller@test.com");
            return CreateService().GetUserAsync(user.Id);
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_ReturnsSuccess()
        {
            var result = await RegisterAdminAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_ReturnsNoError()
        {
            var result = await RegisterAdminAsync();

            Assert.Equal(AuthError.None, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_ReturnsStoreName()
        {
            var result = await RegisterAdminAsync();

            Assert.Equal("GoatShop", result.StoreName);
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_ReturnsIdOfCreatedStore()
        {
            var result = await RegisterAdminAsync();

            Assert.Equal(Query(c => c.Stores.Single().Id), result.StoreId);
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_CreatesOneStore()
        {
            await RegisterAdminAsync();

            Assert.Single(Query(c => c.Stores.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_CreatesOneUser()
        {
            await RegisterAdminAsync();

            Assert.Single(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_UserIsAdmin()
        {
            await RegisterAdminAsync();

            Assert.Equal(UserRole.Admin, Query(c => c.Users.Single().Role));
        }

        [Fact]
        public async Task RegisterAsync_AdminNewStore_UserBelongsToCreatedStore()
        {
            await RegisterAdminAsync();

            Assert.Equal(Query(c => c.Stores.Single().Id), Query(c => c.Users.Single().StoreId));
        }

        [Theory]
        [InlineData("GoatShop")]
        [InlineData(" goatshop ")]
        [InlineData("GOATSHOP")]
        public async Task RegisterAsync_AdminExistingStoreName_ReturnsConflict(string storeName)
        {
            var result = await RegisterAdminWithTakenStoreNameAsync(storeName);

            Assert.Equal(AuthError.Conflict, result.Error);
        }

        [Theory]
        [InlineData("GoatShop")]
        [InlineData(" goatshop ")]
        [InlineData("GOATSHOP")]
        public async Task RegisterAsync_AdminExistingStoreName_DoesNotCreateSecondStore(string storeName)
        {
            await RegisterAdminWithTakenStoreNameAsync(storeName);

            Assert.Single(Query(c => c.Stores.ToList()));
        }

        [Theory]
        [InlineData("GoatShop")]
        [InlineData(" goatshop ")]
        [InlineData("GOATSHOP")]
        public async Task RegisterAsync_AdminExistingStoreName_DoesNotCreateUser(string storeName)
        {
            await RegisterAdminWithTakenStoreNameAsync(storeName);

            Assert.Empty(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_AdminShortStorePassword_ReturnsValidation()
        {
            var result = await RegisterAdminWithShortStorePasswordAsync();

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_AdminShortStorePassword_DoesNotCreateStore()
        {
            await RegisterAdminWithShortStorePasswordAsync();

            Assert.Empty(Query(c => c.Stores.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_ReturnsSuccess()
        {
            var (_, result) = await RegisterSellerIntoExistingStoreAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_ReturnsExistingStoreId()
        {
            var (store, result) = await RegisterSellerIntoExistingStoreAsync();

            Assert.Equal(store.Id, result.StoreId);
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_DoesNotCreateNewStore()
        {
            await RegisterSellerIntoExistingStoreAsync();

            Assert.Single(Query(c => c.Stores.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_CreatesOneUser()
        {
            await RegisterSellerIntoExistingStoreAsync();

            Assert.Single(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_UserIsSeller()
        {
            await RegisterSellerIntoExistingStoreAsync();

            Assert.Equal(UserRole.Seller, Query(c => c.Users.Single().Role));
        }

        [Fact]
        public async Task RegisterAsync_SellerCorrectStorePassword_UserBelongsToExistingStore()
        {
            var (store, _) = await RegisterSellerIntoExistingStoreAsync();

            Assert.Equal(store.Id, Query(c => c.Users.Single().StoreId));
        }

        [Fact]
        public async Task RegisterAsync_SellerWrongStorePassword_ReturnsForbidden()
        {
            var result = await RegisterSellerWithWrongStorePasswordAsync();

            Assert.Equal(AuthError.Forbidden, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_SellerWrongStorePassword_DoesNotCreateUser()
        {
            await RegisterSellerWithWrongStorePasswordAsync();

            Assert.Empty(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_SellerUnknownStore_ReturnsValidation()
        {
            var result = await CreateService().RegisterAsync(SellerRequest("Unknown Store"));

            Assert.Equal(AuthError.Validation, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_SellerUnknownStore_ReturnsStoreNotFoundMessage()
        {
            var result = await CreateService().RegisterAsync(SellerRequest("Unknown Store"));

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
            var result = await RegisterDuplicateEmailInOtherCaseAsync();

            Assert.Equal(AuthError.Conflict, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmailDifferentCase_DoesNotCreateSecondUser()
        {
            await RegisterDuplicateEmailInOtherCaseAsync();

            Assert.Single(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmailDifferentCase_DoesNotCreateSecondStore()
        {
            await RegisterDuplicateEmailInOtherCaseAsync();

            Assert.Single(Query(c => c.Stores.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_EmailWithSpacesAndUppercase_ReturnsNormalizedEmail()
        {
            var result = await CreateService().RegisterAsync(AdminRequest(email: "  Owner@Gmail.COM "));

            Assert.Equal("owner@gmail.com", result.Email);
        }

        [Fact]
        public async Task RegisterAsync_EmailWithSpacesAndUppercase_SavesNormalizedEmail()
        {
            await CreateService().RegisterAsync(AdminRequest(email: "  Owner@Gmail.COM "));

            Assert.Equal("owner@gmail.com", Query(c => c.Users.Single().Email));
        }

        [Fact]
        public async Task RegisterAsync_Password_IsNotStoredAsPlainText()
        {
            await RegisterAdminAsync();

            Assert.NotEqual("password123", Query(c => c.Users.Single().PasswordHash));
        }

        [Fact]
        public async Task RegisterAsync_Password_IsStoredAsMatchingBCryptHash()
        {
            await RegisterAdminAsync();

            Assert.True(BCrypt.Net.BCrypt.Verify("password123", Query(c => c.Users.Single().PasswordHash)));
        }

        [Fact]
        public async Task RegisterAsync_StorePassword_IsNotStoredAsPlainText()
        {
            await RegisterAdminAsync();

            Assert.NotEqual(StoreCode, Query(c => c.Stores.Single().StorePasswordHash));
        }

        [Fact]
        public async Task RegisterAsync_StorePassword_IsStoredAsMatchingBCryptHash()
        {
            await RegisterAdminAsync();

            Assert.True(BCrypt.Net.BCrypt.Verify(StoreCode, Query(c => c.Stores.Single().StorePasswordHash)));
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsSuccess()
        {
            var (_, _, result) = await LoginAsOwnerAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsUserId()
        {
            var (_, user, result) = await LoginAsOwnerAsync();

            Assert.Equal(user.Id, result.UserId);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsEmail()
        {
            var (_, _, result) = await LoginAsOwnerAsync();

            Assert.Equal("owner@test.com", result.Email);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsRole()
        {
            var (_, _, result) = await LoginAsOwnerAsync();

            Assert.Equal(UserRole.Admin, result.Role);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsStoreId()
        {
            var (store, _, result) = await LoginAsOwnerAsync();

            Assert.Equal(store.Id, result.StoreId);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsStoreName()
        {
            var (_, _, result) = await LoginAsOwnerAsync();

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
        public async Task LoginAsync_WrongPassword_IsNotSuccessful()
        {
            var result = await LoginWithWrongPasswordAsync();

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
        {
            var result = await LoginWithWrongPasswordAsync();

            Assert.Equal(AuthError.Unauthorized, result.Error);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsNoUserId()
        {
            var result = await LoginWithWrongPasswordAsync();

            Assert.Null(result.UserId);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ReturnsSameErrorAsWrongPassword()
        {
            var (wrongPassword, unknownEmail) = await LoginWithWrongPasswordAndUnknownEmailAsync();

            Assert.Equal(wrongPassword.Error, unknownEmail.Error);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ReturnsSameMessageAsWrongPassword()
        {
            var (wrongPassword, unknownEmail) = await LoginWithWrongPasswordAndUnknownEmailAsync();

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
            var result = await RegisterSellerWhileParallelRequestTakesEmailAsync();

            Assert.Equal(AuthError.Conflict, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_EmailTakenByParallelRequest_ReturnsEmailTakenMessage()
        {
            var result = await RegisterSellerWhileParallelRequestTakesEmailAsync();

            Assert.Equal("Користувач із таким Email вже існує.", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_EmailTakenByParallelRequest_KeepsOnlyFirstUser()
        {
            await RegisterSellerWhileParallelRequestTakesEmailAsync();

            Assert.Single(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_StoreNameTakenByParallelRequest_ReturnsConflict()
        {
            var result = await RegisterAdminWhileParallelRequestTakesStoreNameAsync();

            Assert.Equal(AuthError.Conflict, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_StoreNameTakenByParallelRequest_ReturnsStoreNameTakenMessage()
        {
            var result = await RegisterAdminWhileParallelRequestTakesStoreNameAsync();

            Assert.StartsWith("Магазин із такою назвою вже існує", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_StoreNameTakenByParallelRequest_KeepsOnlyFirstStore()
        {
            await RegisterAdminWhileParallelRequestTakesStoreNameAsync();

            Assert.Single(Query(c => c.Stores.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_StoreNameTakenByParallelRequest_DoesNotCreateUser()
        {
            await RegisterAdminWhileParallelRequestTakesStoreNameAsync();

            Assert.Empty(Query(c => c.Users.ToList()));
        }

        [Fact]
        public async Task RegisterAsync_OtherDbError_IsRethrown()
        {
            var interceptor = new BeforeSaveInterceptor(() => throw new DbUpdateException("disk full"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => new AuthService(_db.CreateContext(interceptor)).RegisterAsync(AdminRequest()));
        }

        [Fact]
        public async Task GetUserAsync_ExistingUser_ReturnsSuccess()
        {
            var result = await GetExistingSellerAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GetUserAsync_ExistingUser_ReturnsEmail()
        {
            var result = await GetExistingSellerAsync();

            Assert.Equal("seller@test.com", result.Email);
        }

        [Fact]
        public async Task GetUserAsync_ExistingUser_ReturnsRole()
        {
            var result = await GetExistingSellerAsync();

            Assert.Equal(UserRole.Seller, result.Role);
        }

        [Fact]
        public async Task GetUserAsync_ExistingUser_ReturnsStoreName()
        {
            var result = await GetExistingSellerAsync();

            Assert.Equal("GoatShop", result.StoreName);
        }

        [Fact]
        public async Task GetUserAsync_UnknownId_ReturnsUnauthorized()
        {
            var result = await CreateService().GetUserAsync(Guid.NewGuid());

            Assert.Equal(AuthError.Unauthorized, result.Error);
        }

        [Fact]
        public async Task GetUserAsync_UnknownId_ReturnsSessionInvalidMessage()
        {
            var result = await CreateService().GetUserAsync(Guid.NewGuid());

            Assert.StartsWith("Сесія недійсна", result.Message);
        }
    }
}
