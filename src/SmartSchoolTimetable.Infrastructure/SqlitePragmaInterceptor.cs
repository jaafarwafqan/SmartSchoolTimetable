using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartSchoolTimetable.Infrastructure;

/// <summary>
/// Applies per-connection SQLite pragmas each time EF Core opens a connection. Pooling is disabled,
/// so every logical open is a new physical connection that would otherwise use SQLite's defaults.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public const string PragmaCommandText = "PRAGMA synchronous=FULL;";

    public static SqlitePragmaInterceptor Instance { get; } = new();

    private SqlitePragmaInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(connection, cancellationToken);

    public static void Apply(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var command = connection.CreateCommand();
        command.CommandText = PragmaCommandText;
        command.ExecuteNonQuery();
    }

    public static async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = PragmaCommandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
