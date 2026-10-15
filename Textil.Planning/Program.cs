using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Textil.Planning.Data;
using Textil.Planning.Domain;
using Textil.Planning.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, log) => log
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        ctx.Configuration["Logs:Path"] ?? "logs/textil-planning-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<User, IdentityRole>(IdentitySetup.Configure)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<SerbianIdentityErrorDescriber>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "Textil.Planning.Auth";
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
});
// Role changes made in admin reach active sessions within a minute.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

var authorization = builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
foreach (var role in Roles.All)
    authorization.AddPolicy(role, p => p.RequireRole(role));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", Roles.Director);
    o.Conventions.AuthorizeFolder("/Director", Roles.Director);
    o.Conventions.AuthorizeFolder("/Procurement", Roles.Procurement);
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    o.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IAuthService, IdentityAuthService>();
builder.Services.AddSingleton(new LocalTime(
    TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["App:TimeZone"] ?? "Europe/Belgrade")));
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<AccessGuard>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<CycleService>();
builder.Services.AddScoped<ImportService>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
