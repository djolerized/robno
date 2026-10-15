using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Textil.Planning.Data;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Tests;

/// <summary>Real Identity + EF stack over an in-memory SQLite database, one per test.</summary>
public sealed class TestServices : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _root;

    public AsyncServiceScope Scope { get; }

    private TestServices(SqliteConnection connection, ServiceProvider root)
    {
        _connection = connection;
        _root = root;
        Scope = root.CreateAsyncScope();
    }

    public T Get<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();

    public static async Task<TestServices> CreateAsync(bool seedUsers = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityCore<User>(IdentitySetup.Configure)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<SerbianIdentityErrorDescriber>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<AccessGuard>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<CycleService>();
        services.AddScoped<ImportService>();

        var test = new TestServices(connection, services.BuildServiceProvider());
        await test.Get<AppDbContext>().Database.EnsureCreatedAsync();
        await DevSeeder.EnsureRolesAsync(test.Get<RoleManager<IdentityRole>>());
        if (seedUsers)
            await DevSeeder.SeedUsersAsync(test.Get<UserManager<User>>(), Password);
        return test;
    }

    public const string Password = "Textil123!";

    public async Task<string> IdOf(string userName) =>
        (await Get<UserManager<User>>().FindByNameAsync(userName))!.Id;

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await _root.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
