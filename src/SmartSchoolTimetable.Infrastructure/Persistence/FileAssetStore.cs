using System.Text.RegularExpressions;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>
/// Stores uploaded school images in an "assets" folder next to the database (outside the repository and the
/// web root). Files are only reachable through the authenticated API; stored names are generated here and
/// validated on every read, so a crafted name can never escape the folder.
/// </summary>
public sealed partial class FileAssetStore(string rootDirectory) : IAssetStore
{
    public string RootDirectory { get; } = Path.GetFullPath(rootDirectory);

    public async Task<string> SaveAsync(string prefix, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}.{extension}";
        if (!IsStoredName(name))
            throw new ArgumentException("Unsupported asset prefix or extension.", nameof(prefix));
        Directory.CreateDirectory(RootDirectory);
        var path = Path.Combine(RootDirectory, name);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await file.WriteAsync(content, cancellationToken);
        return name;
    }

    public Stream? OpenRead(string storedFileName)
    {
        if (!IsStoredName(storedFileName))
            return null;
        var path = Path.Combine(RootDirectory, storedFileName);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public void Delete(string storedFileName)
    {
        if (!IsStoredName(storedFileName))
            return;
        var path = Path.Combine(RootDirectory, storedFileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static bool IsStoredName(string name) => StoredName().IsMatch(name);

    [GeneratedRegex("^(logo|stamp)-[0-9a-f]{32}\\.(png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex StoredName();
}
