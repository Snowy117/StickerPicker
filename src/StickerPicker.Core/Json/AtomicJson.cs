using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickerPicker.Core.Json;

/// <summary>
/// Atomic JSON read/write helpers (temp file + replace).
/// </summary>
public static class AtomicJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T LoadOrCreate<T>(string path, Func<T> factory, Action<string, Exception>? onCorrupt = null)
        where T : class
    {
        if (!File.Exists(path))
        {
            var created = factory();
            Save(path, created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, s_options)
                ?? throw new JsonException("Deserialized null document.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            onCorrupt?.Invoke(path, ex);
            TryBackupCorrupt(path);
            var created = factory();
            Save(path, created);
            return created;
        }
    }

    public static void Save<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(value, s_options);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);

        // File.Replace requires destination to exist on some platforms; fall back to Move.
        if (File.Exists(path))
        {
            File.Replace(temp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    private static void TryBackupCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backup = path + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            File.Copy(path, backup, overwrite: true);
        }
        catch
        {
            // Best-effort backup only.
        }
    }
}
