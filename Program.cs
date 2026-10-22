using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// API Controllers
builder.Services.AddControllers();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<LogiTrackContext>();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt:Key with at least 32 bytes in local configuration, user secrets or Jwt__Key.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Configure Jwt:Issuer."),
        ValidAudience = builder.Configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Configure Jwt:Audience."),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        NameClaimType = "sub",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();

// Database dependency injection
builder.Services.AddDbContext<LogiTrackContext>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
var app = builder.Build();


// Local administration only: dotnet run -- --assign-manager person@example.com
// This command assigns the role to an existing account and exits without starting HTTP.
using (var scope = app.Services.CreateScope())
{
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync("Manager"))
    {
        var created = await roles.CreateAsync(new IdentityRole("Manager"));
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
    }

    var managerEmail = builder.Configuration["assign-manager"];
    if (!string.IsNullOrWhiteSpace(managerEmail))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(managerEmail)
            ?? throw new InvalidOperationException("Register this user before assigning the Manager role.");
        if (!await users.IsInRoleAsync(user, "Manager"))
        {
            var assigned = await users.AddToRoleAsync(user, "Manager");
            if (!assigned.Succeeded)
                throw new InvalidOperationException(string.Join("; ", assigned.Errors.Select(e => e.Description)));
        }
        Console.WriteLine("Manager role assigned. Log in again to receive a token with this role.");
        return;
    }
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed sample inventory only; orders are created through the API
using (var scope = app.Services.CreateScope())
using (var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>())
{
    // Add sample inventory items if database is empty
    if (!context.InventoryItems.Any())
    {
        context.InventoryItems.AddRange(
            new InventoryItem
            {
                Name = "Pallet Jack",
                Quantity = 12,
                Location = "Warehouse A"
            },
            new InventoryItem
            {
                Name = "Forklift",
                Quantity = 5,
                Location = "Warehouse B"
            }
        );

        context.SaveChanges();
    }



}

//okok

app.Run();
