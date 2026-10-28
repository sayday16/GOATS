using GoatStore.API.Server.Auth;
using GoatStore.API.Server.Products;
using GoatStore.Core.Interfaces;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Не задано ConnectionStrings:DefaultConnection. Задайте його через dotnet user-secrets (див. README).");

builder.Services.AddDbContext<GoatStoreDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddGoatAuth(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapDefaultEndpoints();
app.UseFileServer();

if (!app.Environment.IsEnvironment("Testing"))
{
    await app.WarmUpAuthAsync();
}

app.Run();
public partial class Program { }
