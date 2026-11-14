using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipHarbor;

public enum MediaKind { Video, Audio }

public enum ItemStatus { Queued, Downloading, Paused, Done, Failed, Canceled }

/// <summary>What to download and how. Used per item, for Smart Mode and for subscriptions.</summary>
public class DownloadOptions
{
    public MediaKind Kind { get; set; } = MediaKind.Video;
    /// <summary>Container: mp4/mkv/webm for video, mp3/m4a/opus/wav/flac for audio.</summary>
    public string Format { get; set; } = "mp4";
    /// <summary>Max video height, 0 = best available.</summary>
    public int MaxHeight { get; set; } = 1080;
    public bool Subtitles { get; set; }
    public bool EmbedSubtitles { get; set; } = true;
    public List<string> SubtitleLanguages { get; set; } = new() { "en" };
    public string Folder { get; set; } = "";
    public bool PlaylistSubfolder { get; set; } = true;

    public DownloadOptions Clone()
    {
        var c = (DownloadOptions)MemberwiseClone();
        c.SubtitleLanguages = new List<string>(SubtitleLanguages);
        return c;
    }

    public string Describe()
    {
        if (Kind == MediaKind.Audio) return $"{Format.ToUpperInvariant()} audio";
        var q = MaxHeight <= 0 ? "Best" : QualityLabel(MaxHeight);
        return $"{Format.ToUpperInvariant()} · {q}";
    }

    public static string QualityLabel(int h) => h switch
    {
        >= 4320 => "8K",
        >= 2160 => "4K",
        >= 1440 => "1440p",
        _ => h + "p"
    };
}

public class DownloadItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Uploader { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public double DurationSeconds { get; set; }
    public string Playlist { get; set; } = "";
    public DownloadOptions Options { get; set; } = new();
    public ItemStatus Status { get; set; } = ItemStatus.Queued;
    public string Message { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long TotalBytes { get; set; }
    public long DoneBytes { get; set; }
    public bool Direct { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;

    [JsonIgnore] public double Progress { get; set; }
    [JsonIgnore] public double Speed { get; set; }
    [JsonIgnore] public double Eta { get; set; } = -1;
    [JsonIgnore] public string Phase { get; set; } = "";
}

public class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public DownloadOptions Options { get; set; } = new();
    public HashSet<string> Seen { get; set; } = new();
    public DateTime LastChecked { get; set; }
    public string LastResult { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class AppSettings
{
    public string Theme { get; set; } = "Light";
    public int MaxConcurrent { get; set; } = 3;
    public string SpeedLimit { get; set; } = "";
    public string Proxy { get; set; } = "";
    public string FileNameTemplate { get; set; } = "%(title)s.%(ext)s";
    public string CookiesBrowser { get; set; } = "";
    public bool EmbedThumbnail { get; set; } = true;
    public bool EmbedMetadata { get; set; } = true;
    public bool SkipExisting { get; set; } = true;
    public int SubscriptionCheckHours { get; set; } = 6;
    public string EnginePath { get; set; } = "";
    public string FfmpegPath { get; set; } = "";
    public bool SmartMode { get; set; }
    public DownloadOptions SmartOptions { get; set; } = new();
    public DownloadOptions LastOptions { get; set; } = new();
    public bool NotifyWhenDone { get; set; } = true;
}

public class AppState
{
    public AppSettings Settings { get; set; } = new();
    public List<DownloadItem> Items { get; set; } = new();
    public List<Subscription> Subscriptions { get; set; } = new();

    public static string DataDir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipHarbor");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    static string FilePath => Path.Combine(DataDir, "state.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrEmpty(videos)) videos = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(videos, "ClipHarbor");
    }

    public static AppState Load()
    {
        AppState s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Json) ?? new AppState()
                : new AppState();
        }
        catch { s = new AppState(); }

        foreach (var o in new[] { s.Settings.SmartOptions, s.Settings.LastOptions })
            if (string.IsNullOrWhiteSpace(o.Folder)) o.Folder = DefaultFolder();
        foreach (var it in s.Items)
            if (it.Status == ItemStatus.Downloading) it.Status = ItemStatus.Paused;
        return s;
    }

    readonly object _saveLock = new();

    public void Save()
    {
        try
        {
            string text;
            lock (_saveLock) text = JsonSerializer.Serialize(this, Json);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, FilePath, true);
        }
        catch { /* never crash on save */ }
    }
}

public static class Fmt
{
    public static string Bytes(double b)
    {
        if (b <= 0) return "";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i == 0 ? $"{b:0} {u[i]}" : $"{b:0.#} {u[i]}";
    }

    public static string Duration(double s)
    {
        if (s <= 0) return "";
        var t = TimeSpan.FromSeconds(Math.Round(s));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }
}
