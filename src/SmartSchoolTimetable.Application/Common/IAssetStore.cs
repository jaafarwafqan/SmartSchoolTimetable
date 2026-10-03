namespace SmartSchoolTimetable.Application.Common;

/// <summary>File storage for uploaded school images, outside the repository and outside the web root.</summary>
public interface IAssetStore
{
    /// <summary>Writes the bytes to a new uniquely named file and returns its stored name.</summary>
    Task<string> SaveAsync(string prefix, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Opens a stored file, or returns null when it does not exist or the name is not a stored name.</summary>
    Stream? OpenRead(string storedFileName);

    void Delete(string storedFileName);
}
