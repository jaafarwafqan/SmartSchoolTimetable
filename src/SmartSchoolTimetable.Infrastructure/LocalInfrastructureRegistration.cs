using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Infrastructure;

public static class LocalInfrastructureRegistration
{
    public static IServiceCollection AddLocalInfrastructure(
        this IServiceCollection services,
        string databasePath,
        bool skipLoginDelay = false)
    {
        var fullDatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDatabasePath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            ForeignKeys = true,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();

        services.AddDbContext<LocalDbContext>(options =>
            options
                .UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(typeof(LocalDbContext).Assembly.FullName))
                .EnableSensitiveDataLogging(false));
        services.AddScoped<IOwnerRepository, LocalOwnerRepository>();
        services.AddSingleton<ICredentialHasher, Pbkdf2CredentialHasher>();
        services.AddSingleton<ILocalSessionStore, LocalSessionStore>();
        services.AddSingleton<ILoginDelay>(skipLoginDelay ? new NoLoginDelay() : new RealLoginDelay());
        return services;
    }

    public static async Task InitializeLocalDatabaseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed class NoLoginDelay : ILoginDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
