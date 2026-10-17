using System.ComponentModel.DataAnnotations;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    public class AuthService : IAuthService
    {
        private const int MaxEmailLength = 254;
        private const int MinPasswordLength = 8;
        private const int MaxPasswordLength = 128;
        private const int MaxStoreNameLength = 100;

        private static readonly EmailAddressAttribute EmailValidator = new();

        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing");

        private readonly GoatStoreDbContext _context;

        public AuthService(GoatStoreDbContext context)
        {
            _context = context;
        }

        public void Dispose() => _context.Dispose();

        public async Task<AuthResponseDto> RegisterAsync(
            RegisterRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request.Role is not { } role || (role != UserRole.Admin && role != UserRole.Seller))
                return Fail(AuthError.Validation, "Оберіть коректну роль: власник магазину або працівник.");

            var email = NormalizeEmail(request.Email);
            var storeName = (request.StoreName ?? string.Empty).Trim();
            var storeKey = storeName.ToLowerInvariant();

            var validationError = ValidateRegistration(request, email, storeName);
            if (validationError is not null)
                return Fail(AuthError.Validation, validationError);

            if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
                return Fail(AuthError.Conflict, "Користувач із таким Email вже існує.");

            Store store;

            if (role == UserRole.Admin)
            {
                if (request.StorePassword.Length < MinPasswordLength)
                    return Fail(AuthError.Validation,
                        $"Секретний код магазину має містити щонайменше {MinPasswordLength} символів.");

                if (await _context.Stores.AnyAsync(s => s.NormalizedName == storeKey, cancellationToken))
                    return Fail(AuthError.Conflict, "Магазин із такою назвою вже існує! Придумайте іншу назву.");

                store = new Store
                {
                    Name = storeName,
                    NormalizedName = storeKey,
                    StorePasswordHash = BCrypt.Net.BCrypt.HashPassword(request.StorePassword)
                };

                _context.Stores.Add(store);
            }
            else // UserRole.Seller
            {
                var existingStore = await _context.Stores
                    .FirstOrDefaultAsync(s => s.NormalizedName == storeKey, cancellationToken);

                if (existingStore is null)
                    return Fail(AuthError.Validation,
                        "Магазин не знайдено! Працівник не може зареєструватися, поки власник не створить магазин.");

                if (!BCrypt.Net.BCrypt.Verify(request.StorePassword, existingStore.StorePasswordHash))
                    return Fail(AuthError.Forbidden, "Невірний секретний код (пароль) магазину!");

                store = existingStore;
            }

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                Store = store
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();

                if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
                    return Fail(AuthError.Conflict, "Користувач із таким Email вже існує.");

                if (role == UserRole.Admin &&
                    await _context.Stores.AnyAsync(s => s.NormalizedName == storeKey, cancellationToken))
                    return Fail(AuthError.Conflict, "Магазин із такою назвою вже існує! Придумайте іншу назву.");

                throw; 
            }

            var message = role == UserRole.Admin
                ? $"Магазин '{store.Name}' створено, власника зареєстровано!"
                : $"Ви успішно приєдналися до магазину '{store.Name}' як працівник!";

            return Success(user, store, message);
        }

        public async Task<AuthResponseDto> LoginAsync(
            LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
                return Fail(AuthError.Validation, "Email та пароль є обов'язковими.");

            var email = NormalizeEmail(request.Email);

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            var passwordValid = BCrypt.Net.BCrypt.Verify(
                request.Password, user?.PasswordHash ?? DummyPasswordHash);

            if (user is null || !passwordValid)
                return Fail(AuthError.Unauthorized, "Невірний Email або пароль.");

            return Success(user, user.Store, "Вхід виконано успішно!");
        }

        private static string? ValidateRegistration(RegisterRequestDto request, string email, string storeName)
        {
            if (email.Length == 0 || email.Length > MaxEmailLength || !EmailValidator.IsValid(email))
                return "Вкажіть коректний Email.";

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                return $"Пароль має містити щонайменше {MinPasswordLength} символів.";

            if (request.Password.Length > MaxPasswordLength)
                return $"Пароль занадто довгий (максимум {MaxPasswordLength} символів).";

            if (request.Password != request.ConfirmPassword)
                return "Паролі не збігаються.";

            if (storeName.Length == 0 || string.IsNullOrWhiteSpace(request.StorePassword))
                return "Вкажіть назву та секретний код магазину.";

            if (storeName.Length > MaxStoreNameLength)
                return $"Назва магазину занадто довга (максимум {MaxStoreNameLength} символів).";

            return null;
        }

        private static string NormalizeEmail(string? email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        private static AuthResponseDto Fail(AuthError error, string message) => new()
        {
            IsSuccess = false,
            Error = error,
            Message = message
        };

        private static AuthResponseDto Success(User user, Store? store, string message) => new()
        {
            IsSuccess = true,
            Message = message,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            StoreId = store?.Id,
            StoreName = store?.Name
        };
    }
}