using System.Threading.RateLimiting;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GoatStore.API.Server.Auth
{
    public static class AuthSetup
    {
        public const string AdminOnlyPolicy = "AdminOnly";
        public const string AuthRateLimitPolicy = "auth";

        public static IServiceCollection AddGoatAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            var authPermitLimit = configuration.GetValue<int?>("RateLimiting:AuthPermitLimit")
                ?? (environment.IsDevelopment() ? 100 : 10);

            services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<JwtTokenService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
                {
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = jwt.Value.Issuer,
                        ValidAudience = jwt.Value.Audience,
                        IssuerSigningKey = jwt.Value.CreateSigningKey(),
                        ClockSkew = TimeSpan.FromMinutes(1),
                        NameClaimType = GoatClaims.Email,
                        RoleClaimType = GoatClaims.Role
                    };
                });

            services.AddAuthorizationBuilder()
                .AddPolicy(AdminOnlyPolicy, policy => policy.RequireRole("Admin"));

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, cancellationToken) => new ValueTask(
                    context.HttpContext.Response.WriteAsJsonAsync(
                        new AuthResponseDto { IsSuccess = false, Message = "Забагато спроб. Спробуйте ще раз за хвилину." },
                        cancellationToken));

                options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            });

            return services;
        }

        public static async Task WarmUpAuthAsync(this WebApplication app)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                await auth.LoginAsync(new LoginRequestDto { Email = "warmup@goatstore.local", Password = "warmup-password" });
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Auth warm-up failed");
            }
        }
    }
}
