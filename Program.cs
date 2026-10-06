using TSC.Data;
using TSC.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Grading is the one bit of real arithmetic in here; `dotnet run -- selftest` checks it
// without dragging in a test framework.
if (args.Contains("selftest"))
{
    GradingSelfTest.Run();
    PhoneSelfTest.Run();
    ClockSelfTest.Run();
    ConnectionStringSelfTest.Run();
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL + Entity Framework Core.
// The connection string is deliberately NOT in appsettings.json — it carries the database
// password. Development reads it from user-secrets, deployment from the environment:
//   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."
//   ConnectionStrings__DefaultConnection=Host=...;Password=...
// Hosts that link a database automatically (Render, Heroku) set DATABASE_URL instead, in
// URL form; ConnectionString.Normalize turns that into what Npgsql expects.
var connectionString = ConnectionString.Normalize(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException(
        "No 'DefaultConnection' connection string. Set it with `dotnet user-secrets set " +
        "\"ConnectionStrings:DefaultConnection\" \"<value>\"` or the " +
        "ConnectionStrings__DefaultConnection environment variable. See README.md."));

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

// A host like Render terminates TLS at its edge and forwards plain HTTP. Without this the
// app sees http, UseHttpsRedirection bounces the request straight back out, and the browser
// loops. The proxy is not on a loopback address, so the default known-network check has to go.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseForwardedHeaders();

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
    // On a host there is no shell to run `dotnet ef database update` from, and the seeding
    // below needs the tables to exist. Migrating on startup is safe: EF only applies what
    // the database has not seen yet.
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

    // Roles are structural — every environment needs them.
    await DbSeeder.SeedRolesAsync(scope.ServiceProvider);

    // The default admin account is a published credential. Seeding it outside Development
    // would hand anyone who has seen this repo a way in, so it is deliberately dev-only.
    if (app.Environment.IsDevelopment())
        await DbSeeder.SeedDevelopmentAdminAsync(scope.ServiceProvider);
}

// `dotnet run -- create-admin <email> <password> [full name]`
// The only way to make the first admin on a server, now that the default one is dev-only.
if (args.Length >= 3 && args[0] == "create-admin")
{
    using var scope = app.Services.CreateScope();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var email = args[1];

    if (await userManager.FindByEmailAsync(email) != null)
    {
        Console.Error.WriteLine($"{email} already exists.");
        return 1;
    }

    var name = args.Length > 3 ? string.Join(' ', args[3..]) : email;
    var failure = await DbSeeder.CreateAdminAsync(userManager, email, args[2], name);

    if (failure != null)
    {
        Console.Error.WriteLine(failure);
        return 1;
    }

    Console.WriteLine($"Admin created: {email}");
    return 0;
}

app.Run();

return 0;