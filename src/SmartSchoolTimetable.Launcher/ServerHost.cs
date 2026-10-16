using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartSchoolTimetable.Launcher;

/// <summary>
/// The local server process (<c>SmartSchoolTimetable.Api.exe</c> next to the launcher): started hidden on 127.0.0.1 with a free port (5080 when it is free)
/// and a fresh random control token that only the launcher knows, stopped through the control channel (never by the browser). The server's own
/// protections (Host and Origin checks, the per-launch token, SameSite cookies) are unchanged: the launcher uses the same rules as a browser.
/// </summary>
internal sealed class ServerHost : IDisposable
{
    public const int PreferredPort = 5080;
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);

    private readonly string controlToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private Process? process;

    public int Port { get; private set; }

    public string Origin => $"http://127.0.0.1:{Port}";

    /// <summary>The data folder of the program (the database, backups, the startup status file); the installer never touches it.</summary>
    /// <remarks>The environment variable SST_DATA_DIR replaces it (for tests and portable use): the server then uses the database in that folder.</remarks>
    public static string DataFolder => Environment.GetEnvironmentVariable("SST_DATA_DIR") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartSchoolTimetable");

    private static bool CustomDataFolder => Environment.GetEnvironmentVariable("SST_DATA_DIR") is { Length: > 0 };

    public static string ServerExecutable => Path.Combine(AppContext.BaseDirectory, "SmartSchoolTimetable.Api.exe");

    /// <summary>5080 when it is free, otherwise any free port (another program may already use 5080).</summary>
    public static int ChoosePort()
    {
        foreach (var candidate in new[] { PreferredPort, 0 })
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, candidate);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // Busy: try the next choice.
            }
        }
        throw new InvalidOperationException("No free local port.");
    }

    /// <summary>Starts the server and waits until it answers. Returns null when it is running, otherwise the Arabic reason.</summary>
    public async Task<string?> StartAsync()
    {
        if (!File.Exists(ServerExecutable))
            return Texts.ServerMissing;
        Port = ChoosePort();
        var startedAt = DateTime.Now;
        var info = new ProcessStartInfo(ServerExecutable)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["LocalHost__Port"] = Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        info.Environment["Control__Token"] = controlToken;
        info.Environment["Launcher__ParentPid"] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (CustomDataFolder)
        {
            Directory.CreateDirectory(DataFolder);
            info.Environment["Database__Path"] = Path.Combine(DataFolder, "timetable.db");
        }
        process = Process.Start(info);
        if (process is null)
            return Texts.StartFailed;

        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
                return ReasonFromStatusFile(startedAt) ?? Texts.StartFailed;
            try
            {
                using var response = await http.GetAsync(new Uri($"{Origin}/api/v1/bootstrap"));
                if (response.IsSuccessStatusCode)
                    return null;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // Slow answer: keep waiting.
            }
            await Task.Delay(300);
        }
        return Texts.StartFailed;
    }

    /// <summary>The server writes an Arabic reason to startup-error.txt when the data could not be upgraded (docs/UPGRADE_AR.md).</summary>
    private static string? ReasonFromStatusFile(DateTime startedAt)
    {
        try
        {
            var file = Path.Combine(DataFolder, "startup-error.txt");
            if (File.Exists(file) && File.GetLastWriteTime(file) >= startedAt.AddSeconds(-2))
                return $"{Texts.StartFailedWithReason}{Environment.NewLine}{File.ReadAllText(file).Trim()}";
        }
        catch (IOException)
        {
            // Unreadable: the generic message is used.
        }
        return null;
    }

    private async Task<HttpRequestMessage> ControlRequestAsync(string path)
    {
        using var bootstrap = await http.GetAsync(new Uri($"{Origin}/api/v1/bootstrap"));
        using var document = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync());
        var launchToken = document.RootElement.GetProperty("launchToken").GetString() ?? string.Empty;
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{Origin}/api/v1/control/{path}")) { Content = new StringContent(string.Empty) };
        request.Headers.TryAddWithoutValidation("Origin", Origin);
        request.Headers.TryAddWithoutValidation("X-Local-Launch-Token", launchToken);
        request.Headers.TryAddWithoutValidation("X-Control-Token", controlToken);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    /// <summary>«نسخة احتياطية الآن»: true when a copy was written.</summary>
    public async Task<bool> BackupNowAsync()
    {
        try
        {
            using var request = await ControlRequestAsync("backup-now");
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Asks the server to stop (a running generation is interrupted and recovered at the next start), waits, and ends it if it does not.</summary>
    public async Task StopAsync()
    {
        var running = process;
        if (running is null || running.HasExited)
            return;
        try
        {
            using var request = await ControlRequestAsync("stop");
            using var response = await http.SendAsync(request);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            // The server may already be going down.
        }
        if (!running.WaitForExit(15_000))
            running.Kill(entireProcessTree: true);
    }

    public void Dispose()
    {
        try
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        process?.Dispose();
        http.Dispose();
    }
}
