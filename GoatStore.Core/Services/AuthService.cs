using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly GoatStoreDbContext _context;

        public AuthService(GoatStoreDbContext context)
        {
            _context = context;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Fail("Email та пароль є обов'язковими.");

            if (request.Password != request.ConfirmPassword)
                return Fail("Паролі не збігаються.");

            if (string.IsNullOrWhiteSpace(request.StoreName) || string.IsNullOrWhiteSpace(request.StorePassword))
                return Fail("Вкажіть назву та секретний код магазину.");

            var normalizedEmail = request.Email.Trim().ToLower();
            var normalizedStoreName = request.StoreName.Trim();

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return Fail("Користувач із таким Email вже існує.");

            Store? targetStore = null;

            if (request.Role == UserRole.Admin)
            {
                bool storeExists = await _context.Stores
                    .AnyAsync(s => s.Name.ToLower() == normalizedStoreName.ToLower());

                if (storeExists)
                    return Fail("Магазин із такою назвою вже існує! Придумайте іншу назву.");

                targetStore = new Store
                {
                    Name = normalizedStoreName,
                    StorePasswordHash = BCrypt.Net.BCrypt.HashPassword(request.StorePassword)
                };

                _context.Stores.Add(targetStore);
            }
            else if (request.Role == UserRole.Seller)
            {
                targetStore = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == normalizedStoreName.ToLower());

                if (targetStore == null)
                    return Fail("Магазин не знайдено! Працівник не може зареєструватися, поки власник не створить магазин.");

                if (!BCrypt.Net.BCrypt.Verify(request.StorePassword, targetStore.StorePasswordHash))
                    return Fail("Невірний секретний код (пароль) магазину!");
            }

            var newUser = new User
            {
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                Store = targetStore! 
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = request.Role == UserRole.Admin
                    ? $"Магазин '{targetStore!.Name}' створено, власника зареєстровано!"
                    : $"Ви успішно приєдналися до магазину '{targetStore!.Name}' як працівник!",
                UserId = newUser.Id,
                Email = newUser.Email,
                Role = newUser.Role,
                StoreId = targetStore!.Id,
                StoreName = targetStore.Name
            };
        }

        private AuthResponseDto Fail(string message) => new() { IsSuccess = false, Message = message };

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users
                .Include(u => u.Store) 
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Fail("Невірний Email або пароль.");

            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Вхід виконано успішно!",
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                StoreId = user.StoreId,
                StoreName = user.Store?.Name
            };
        }
    }
}