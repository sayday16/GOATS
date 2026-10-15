using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<GoatStoreDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

app.UseExceptionHandler();

var authGroup = app.MapGroup("/api/auth");

authGroup.MapPost("/register", async (RegisterRequestDto request, IAuthService authService) =>
{
    var result = await authService.RegisterAsync(request);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

authGroup.MapPost("/login", async (LoginRequestDto request, IAuthService authService) =>
{
    var result = await authService.LoginAsync(request);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});


app.MapDefaultEndpoints();
app.UseFileServer();

app.Run();