using TSC.Data;
using TSC.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
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
// the admin keeps working until then. Zero made "Remove login" instant but cost every single
// navigation a DB round trip before the page's own queries even ran — on Render that stacked
// up into the slow redirects users were hitting. 30 seconds keeps revocation fast enough while
// only paying that cost once every 30s per user instead of on every click.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromSeconds(30));

// A host like Render terminates TLS at its edge and forwards plain HTTP. Without this the
// app sees every request as http: Secure cookies get dropped, generated absolute URLs come
// out http, and HSTS never fires. The proxy is not on a loopback address, so the default
// known-network check has to go.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Bootstrap's CSS/JS and the view HTML are plain text — gzip shrinks them 70-80% for free.
// Render's edge does TLS but not this, so it's on the app.
builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Dev only. The container binds http://0.0.0.0:$PORT and nothing else, so in deployment this
// middleware has no HTTPS port to redirect to — it logs "Failed to determine the https port
// for redirect" and forwards the request unchanged. Render's edge already does http->https,
// so the redirect is its job, not ours. Locally the https launch profile binds port 7178,
// which the middleware reads off the server addresses, so dev redirects as before.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Without a Cache-Control header the browser re-validates every css/js/image on every page
// load (a round trip each, even when the answer is "unchanged"). CSS/JS are cache-busted by
// asp-append-version, so a long max-age is safe for them; images aren't versioned, so a
// week is a reasonable middle ground that still picks up a future logo swap reasonably fast.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
});

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

// Uptime pings land here. No database, no auth, no view — just proof the process is up,
// which is all a keep-warm monitor needs.
app.MapGet("/health", () => Results.Text("OK"));

using (var scope = app.Services.CreateScope())
{
    // Where we actually ended up pointing, minus the password. When a deploy dies on the
    // database this is the line that says whether the connection string was the problem.
    var target = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    // The password length is here because a URL-form connection string silently mangles a
    // password that was not percent-encoded, and "28P01 password authentication failed"
    // looks identical whether the password is wrong or merely truncated at a '#'.
    app.Logger.LogInformation("Postgres target: {Username}@{Host}:{Port}/{Database} (password: {Length} chars)",
        target.Username, target.Host, target.Port, target.Database, target.Password?.Length ?? 0);

    // On a host there is no shell to run `dotnet ef database update` from, and the seeding
    // below needs the tables to exist. Migrating on startup is safe: EF only applies what
    // the database has not seen yet.
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

    // Roles are structural — every environment needs them.
    await DbSeeder.SeedRolesAsync(scope.ServiceProvider);

    // The first admin on a server, from BOOTSTRAP_ADMIN_EMAIL / BOOTSTRAP_ADMIN_PASSWORD.
    // No-op when they are unset, so a normal deploy is unaffected.
    await DbSeeder.SeedAdminFromConfigurationAsync(scope.ServiceProvider, builder.Configuration, app.Logger);

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