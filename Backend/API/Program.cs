using Data.Context;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models;

var bld = WebApplication.CreateBuilder();

var connStr = bld.Configuration.GetConnectionString("AlexandriaPostgres");

bld.Services.AddDbContext<AlexandriaDbContext>(opt => opt.UseNpgsql(connStr));
bld.Services.AddAuthorization();
    ;// Register Identity (no MapIdentityApi)
bld.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AlexandriaDbContext>()
    .AddDefaultTokenProviders();

bld.Services.AddFastEndpoints().SwaggerDocument();
bld.Services.AddHealthChecks();

var app = bld.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints()
    .UseSwaggerGen(); // This enables the Swagger UI;
app.MapHealthChecks("/health");

app.Run();