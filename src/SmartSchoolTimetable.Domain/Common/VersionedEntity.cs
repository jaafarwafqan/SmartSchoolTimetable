namespace SmartSchoolTimetable.Domain.Common;

/// <summary>
/// Base for editable aggregates. <see cref="Version"/> is an optimistic-concurrency token: every mutating
/// operation calls <see cref="Touch"/>, and persistence only updates the row when the stored version still
/// equals the version that was loaded, so a stale edit from another browser tab is rejected.
/// </summary>
public abstract class VersionedEntity
{
    public long Id { get; protected set; }
    public int Version { get; private set; } = 1;

    protected void Touch() => Version++;

    /// <summary>True when the client edited the version it last saw.</summary>
    public bool IsVersion(int expectedVersion) => Version == expectedVersion;
}
