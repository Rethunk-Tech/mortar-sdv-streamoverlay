using System.Text.Json;
using StardewModdingAPI;

namespace StreamOverlay;

/// <summary>What Mortar's stream overlay setting asks of this launch, from <c>startup/overlay.json</c> in the Mortar
/// profile. The keys are Mortar's own.</summary>
internal sealed class OverlayConfig
{
    internal const string FileName = "startup/overlay.json";
    private const int MaxBytes = 65536;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public bool OverlayEnabled { get; set; }

    public int OverlayPort { get; set; }

    public string? OverlayToken { get; set; }

    /// <summary>The request when the profile asks for an overlay it can serve; null otherwise. A profile that asks for
    /// one and cannot get it logs why, since silence looks like a broken overlay.</summary>
    internal static OverlayConfig? Read(string modDirectory, IMonitor monitor)
    {
        if (ProfileDirectory.Find(modDirectory) is not { } profile)
            return null;

        string? text = ReadText(Path.Join(profile, FileName), out string? error);
        if (error != null)
            monitor.Log($"Stream overlay is off: could not read {FileName}: {error}", LogLevel.Warn);

        OverlayConfig? config = Parse(text, out string? why);
        if (why != null)
            monitor.Log($"Stream overlay is off: {why}.", LogLevel.Warn);
        return config;
    }

    /// <summary>The usable request in <paramref name="json"/>, else null: silently when no file or the setting is off,
    /// with a reason when it asks for an overlay it cannot have.</summary>
    internal static OverlayConfig? Parse(string? json, out string? why)
    {
        why = null;
        if (json == null)
            return null;

        OverlayConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<OverlayConfig>(json, Json);
        }
        catch (JsonException ex)
        {
            why = $"{FileName} is not valid JSON: {ex.Message}";
            return null;
        }

        if (config is not { OverlayEnabled: true })
            return null;
        if (!OverlayServer.IsValidPort(config.OverlayPort))
        {
            why = $"OverlayPort {config.OverlayPort} is not a TCP port";
            return null;
        }
        if (string.IsNullOrWhiteSpace(config.OverlayToken))
        {
            why = "OverlayToken is missing";
            return null;
        }
        return config;
    }

    /// <summary>The file's text, or null when it is missing. <paramref name="error"/> is set only when the file exists
    /// and cannot be read.</summary>
    private static string? ReadText(string path, out string? error)
    {
        error = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxBytes)
            {
                error = $"the file is over {MaxBytes} bytes";
                return null;
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = ex.Message;
            return null;
        }
    }
}

/// <summary>The Mortar profile a mod runs in: the nearest folder above the mod holding profile.json.</summary>
internal static class ProfileDirectory
{
    internal static string? Find(string modDirectory)
    {
        DirectoryInfo? current = new(modDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Join(current.FullName, "profile.json")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}
