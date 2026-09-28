using TSC.Data;
using TSC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Grading is the one bit of real arithmetic in here; `dotnet run -- selftest` checks it
// without dragging in a test framework.
if (args.Contains("selftest"))
{
    GradingSelfTest.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL + Entity Framework Core.
// The connection string is deliberately NOT in appsettings.json — it carries the database
// password. Development reads it from user-secrets, deployment from the environment:
//   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."
//   ConnectionStrings__DefaultConnection=Host=...;Password=...
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No 'DefaultConnection' connection string. Set it with `dotnet user-secrets set " +
        "\"ConnectionStrings:DefaultConnection\" \"<value>\"` or the " +
        "ConnectionStrings__DefaultConnection environment variable. See README.md.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Identity re-checks the security stamp every 30 minutes by default, so a login removed by
// the admin keeps working until then. Revalidate every request: one small lookup, and
// "Remove login" actually means removed. Raise this if the user count ever makes it hurt.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}


app.Run();