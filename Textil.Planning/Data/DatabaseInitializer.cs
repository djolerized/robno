using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Textil.Planning.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
            await services.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        await DevSeeder.EnsureRolesAsync(services.GetRequiredService<RoleManager<IdentityRole>>());

        if (app.Environment.IsDevelopment())
        {
            var password = app.Configuration["Seed:Password"]
                ?? throw new InvalidOperationException("Seed:Password is not configured.");
            await DevSeeder.SeedUsersAsync(services.GetRequiredService<UserManager<User>>(), password);
        }
    }
}
