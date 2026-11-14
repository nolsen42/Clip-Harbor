using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ClipHarbor;

public partial class MainForm : Form
{
    readonly AppState _state;
    readonly string? _shot;
    readonly Dictionary<string, CancellationTokenSource> _running = new();
    readonly System.Windows.Forms.Timer _tick = new() { Interval = 250 };
    readonly System.Windows.Forms.Timer _subTimer = new() { Interval = 60_000 };

    readonly Panel _top = new() { Dock = DockStyle.Top };
    readonly NavBar _nav = new() { Dock = DockStyle.Left };
    readonly Panel _main = new() { Dock = DockStyle.Fill };
    readonly Panel _banner = new() { Dock = DockStyle.Top, Visible = false };
    readonly Label _bannerText = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    readonly FlatButton _bannerBtn = new() { Size = new Size(140, 30) };
    readonly FlatButton _bannerClose = new() { Glyph = "", Size = new Size(30, 30) };
    readonly Panel _bottom = new() { Dock = DockStyle.Bottom };
    readonly Label _statusText = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    readonly DownloadList _list = new() { Dock = DockStyle.Fill };
    readonly SubscriptionsView _subs;
    readonly FlatButton _paste = new() { Text = "Paste Link", Glyph = "", Primary = true };
    readonly FlatButton _smart = new() { Text = "Smart Mode", Glyph = "", Toggle = true };
    readonly FlatButton _settingsBtn = new() { Glyph = "" };
    readonly TextBox _search = new() { PlaceholderText = "Search downloads" };
    readonly FlatButton _pauseAll = new() { Text = "Pause all", Glyph = "" };
    readonly FlatButton _resumeAll = new() { Text = "Resume all", Glyph = "" };
    readonly FlatButton _clearDone = new() { Text = "Clear finished", Glyph = "" };
    readonly ToolTip _tip = new();
    readonly NotifyIcon _tray = new();
    CancellationTokenSource? _analyzeCts;
    Action? _bannerAction;
    bool _dirty;
    int _frames;

    float S => DeviceDpi / 96f;

    public MainForm(AppState state, string? screenshotPath)
    {
        _state = state;
        _shot = screenshotPath;
        Engine.Settings = state.Settings;
        Theme.Apply(state.Settings.Theme);

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "ClipHarbor — video & audio downloader";
        Font = Theme.Base;
        Icon = MakeIcon();
        MinimumSize = new Size(820, 520);
        ClientSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;
        DoubleBuffered = true;

        _subs = new SubscriptionsView(state, this) { Dock = DockStyle.Fill, Visible = false };

        BuildTop();
        BuildNav();
        BuildBanner();
        BuildBottom();

        _list.Source = state.Items;
        _list.EmptyTitle = "Copy a video link, then press Paste Link";
        _list.EmptyHint = "Videos, whole playlists, channels, audio and subtitles — as many as you like, in any quality the site offers. No limits, no account.";
        _list.ActionClicked += OnRowAction;
        _list.Opened += it => { if (it.Status == ItemStatus.Done) OpenFile(it); };
        _list.MenuRequested += p => BuildMenu().Show(_list, p);

        _main.Controls.Add(_list);
        _main.Controls.Add(_subs);
        _main.Controls.Add(_banner);
        Controls.Add(_main);
        Controls.Add(_bottom);
        Controls.Add(_nav);
        Controls.Add(_top);

        _tray.Icon = Icon;
        _tray.Text = "ClipHarbor";
        _tray.Visible = false;
        _tray.BalloonTipClicked += (_, _) => { Show(); Activate(); };

        _tick.Tick += (_, _) => Tick();
        _subTimer.Tick += (_, _) => _ = _subs.AutoCheck();
        KeyDown += OnKey;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.Text) == true || e.Data?.GetDataPresent("UniformResourceLocatorW") == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { var t = e.Data?.GetData(DataFormats.UnicodeText) as string ?? e.Data?.GetData(DataFormats.Text) as string; if (t != null) _ = AddLinks(t); };
        Thumbs.Loaded += () => { };

        ApplyTheme();
        Navigate("all");
        CheckEngineBanner();
        _tick.Start();
        if (_shot == null) { _subTimer.Start(); }
        else
        {
            var frame = new System.Windows.Forms.Timer { Interval = 16 };
            frame.Tick += (_, _) => ScreenshotFrame(frame);
            frame.Start();
        }
    }

    // ---------------- layout ----------------

    void BuildTop()
    {
        _top.Height = 64;
        _top.Paint += (_, e) =>
        {
            var g = e.Graphics; float s = S;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var logo = new RectangleF(20 * s, (_top.Height - 32 * s) / 2, 32 * s, 32 * s);
            DrawLogo(g, logo);
            TextRenderer.DrawText(g, "ClipHarbor", Theme.Big, new Point((int)(62 * s), (int)((_top.Height - 30 * s) / 2)), Theme.Text);
            using var pen = new Pen(Theme.Border);
            g.DrawLine(pen, 0, _top.Height - 1, _top.Width, _top.Height - 1);
        };
        _paste.Size = new Size(140, 38);
        _smart.Size = new Size(136, 38);
        _settingsBtn.Size = new Size(38, 38);
        _search.Size = new Size(240, 28);
        _paste.Click += async (_, _) => await PasteFromClipboard();
        _smart.Click += (_, _) => ToggleSmart();
        _settingsBtn.Click += (_, _) => OpenSettings();
        _search.TextChanged += (_, _) => { _list.Search = _search.Text; _list.Rebuild(); };
        _tip.SetToolTip(_paste, "Download the link you copied (Ctrl+V)");
        _tip.SetToolTip(_smart, "One click: download right away with your saved choices");
        _tip.SetToolTip(_settingsBtn, "Settings");
        _top.Controls.AddRange(new Control[] { _paste, _smart, _search, _settingsBtn });
        _top.Resize += (_, _) => LayoutTop();
    }

    void LayoutTop()
    {
        float s = S;
        int y = (_top.Height - _paste.Height) / 2;
        int x = _top.Width - (int)(20 * s) - _settingsBtn.Width;
        _settingsBtn.Location = new Point(x, y);
        x -= (int)(10 * s) + _search.Width;
        _search.Location = new Point(x, (_top.Height - _search.Height) / 2);
        _smart.Location = new Point((int)(230 * s) + _paste.Width + (int)(10 * s), y);
        _paste.Location = new Point((int)(230 * s), y);
        _search.Visible = _search.Left > _smart.Right + 10 * s;
    }

    void BuildNav()
    {
        _nav.Width = 220;
        _nav.Items.AddRange(new[]
        {
            new NavItem { Header = true, Text = "Downloads" },
            new NavItem { Key = "all", Text = "All", Glyph = "" },
            new NavItem { Key = "active", Text = "In progress", Glyph = "" },
            new NavItem { Key = "done", Text = "Finished", Glyph = "" },
            new NavItem { Key = "failed", Text = "Needs attention", Glyph = "" },
            new NavItem { Header = true, Text = "Library" },
            new NavItem { Key = "video", Text = "Videos", Glyph = "" },
            new NavItem { Key = "audio", Text = "Audio", Glyph = "" },
            new NavItem { Key = "playlists", Text = "From playlists", Glyph = "" },
            new NavItem { Header = true, Text = "Follow" },
            new NavItem { Key = "subs", Text = "Subscriptions", Glyph = "" },
        });
        _nav.Footer = "Free and unlimited: no download caps, no playlist limits, no sign-up.";
        _nav.Selected += Navigate;
    }

    void BuildBanner()
    {
        _banner.Height = 52;
        _banner.Controls.AddRange(new Control[] { _bannerText, _bannerBtn, _bannerClose });
        _banner.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, _banner.Height - 1, _banner.Width, _banner.Height - 1);
        };
        _banner.Resize += (_, _) =>
        {
            float s = S;
            _bannerClose.Location = new Point(_banner.Width - _bannerClose.Width - (int)(14 * s), (_banner.Height - _bannerClose.Height) / 2);
            _bannerBtn.Location = new Point(_bannerClose.Left - _bannerBtn.Width - (int)(8 * s), (_banner.Height - _bannerBtn.Height) / 2);
            _bannerText.Location = new Point((int)(20 * s), 0);
            _bannerText.Size = new Size(Math.Max(10, _bannerBtn.Left - (int)(30 * s)), _banner.Height);
        };
        _bannerBtn.Click += (_, _) => _bannerAction?.Invoke();
        _bannerClose.Click += (_, _) => { _analyzeCts?.Cancel(); _banner.Visible = false; };
    }

    void ShowBanner(string text, string? button, Action? action, bool warn = false)
    {
        _bannerText.Text = text;
        _bannerText.ForeColor = Theme.Text;
        _banner.BackColor = warn ? Theme.Mix(Theme.Warn, Theme.Surface, Theme.Dark ? .8 : .88) : Theme.Mix(Theme.Accent, Theme.Surface, Theme.Dark ? .82 : .9);
        _bannerBtn.Visible = button != null;
        if (button != null) { _bannerBtn.Text = button; _bannerBtn.Primary = true; _bannerBtn.Width = Math.Max(_bannerBtn.Measure().Width, (int)(120 * S)); }
        _bannerAction = action;
        _banner.Visible = true;
        _banner.PerformLayout();
        _banner.Height = (int)(52 * S) + 1; _banner.Height = (int)(52 * S);
        _banner.Invalidate();
    }

    void BuildBottom()
    {
        _bottom.Height = 48;
        _bottom.Paint += (_, e) => { using var pen = new Pen(Theme.Border); e.Graphics.DrawLine(pen, 0, 0, _bottom.Width, 0); };
        foreach (var b in new[] { _pauseAll, _resumeAll, _clearDone }) { b.Height = 32; b.Font = Theme.Base; }
        _bottom.Controls.AddRange(new Control[] { _statusText, _pauseAll, _resumeAll, _clearDone });
        _pauseAll.Click += (_, _) => { foreach (var it in _state.Items.Where(i => i.Status is ItemStatus.Downloading or ItemStatus.Queued).ToList()) Pause(it); Changed(); };
        _resumeAll.Click += (_, _) => { foreach (var it in _state.Items.Where(i => i.Status is ItemStatus.Paused or ItemStatus.Failed)) it.Status = ItemStatus.Queued; Changed(); };
        _clearDone.Click += (_, _) => { _state.Items.RemoveAll(i => i.Status is ItemStatus.Done or ItemStatus.Canceled); Changed(); };
        _bottom.Resize += (_, _) => LayoutBottom();
    }

    void LayoutBottom()
    {
        float s = S;
        int x = _bottom.Width - (int)(16 * s);
        foreach (var b in new[] { _clearDone, _resumeAll, _pauseAll })
        {
            b.Width = b.Measure().Width;
            x -= b.Width;
            b.Location = new Point(x, (_bottom.Height - b.Height) / 2);
            x -= (int)(8 * s);
        }
        _statusText.Location = new Point((int)(20 * s), 0);
        _statusText.Size = new Size(Math.Max(10, x - (int)(20 * s)), _bottom.Height);
    }

    public void ApplyTheme()
    {
        Theme.Apply(_state.Settings.Theme);
        BackColor = Theme.Back;
        _top.BackColor = Theme.Surface;
        _bottom.BackColor = Theme.Surface;
        _main.BackColor = Theme.Back;
        _search.BackColor = Theme.InputBack; _search.ForeColor = Theme.Text; _search.BorderStyle = BorderStyle.FixedSingle;
        _statusText.ForeColor = Theme.SubText;
        _subs.ApplyTheme();
        if (IsHandleCreated) Theme.TitleBar(this);
        foreach (Control c in new Control[] { this, _top, _nav, _list, _bottom, _banner, _subs }) c.Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.TitleBar(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutTop(); LayoutBottom();
        _smart.Checked = _state.Settings.SmartMode;
    }

    // ---------------- screenshot mode ----------------

    protected override bool ShowWithoutActivation => _shot != null;

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_shot != null)
            SetWindowPos(Handle, new IntPtr(1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // HWND_BOTTOM, NOSIZE|NOMOVE|NOACTIVATE
        else if (_state.Settings.SubscriptionCheckHours > 0)
            _ = _subs.AutoCheck();
    }

    void ScreenshotFrame(System.Windows.Forms.Timer frame)
    {
        _frames++;
        _list.Invalidate(); _nav.Invalidate(); _top.Invalidate();
        var preview = Environment.GetEnvironmentVariable("CLIPHARBOR_PREVIEW");
        if (_frames == 2 && preview != null) Preview(preview);
        if (_frames < 120) return;
        frame.Stop();
        if (_previewForm != null)
        {
            using var pb = new Bitmap(_previewForm.Width, _previewForm.Height);
            _previewForm.DrawToBitmap(pb, new Rectangle(0, 0, pb.Width, pb.Height));
            pb.Save(Path.ChangeExtension(_shot!, ".dialog.png"), ImageFormat.Png);
            _previewForm.Close();
        }
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(_shot!));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
            bool ok = false;
            using (var g = Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                try { ok = Environment.GetEnvironmentVariable("CLIPHARBOR_SHOT_DRAW") == null && PrintWindow(Handle, hdc, 3); } finally { g.ReleaseHdc(hdc); }
            }
            if (!ok || IsBlank(bmp))
                DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(_shot!, ImageFormat.Png);
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        Close();
    }

    Form? _previewForm;

    /// <summary>Developer preview (screenshot mode only): sample rows and dialogs.</summary>
    void Preview(string what)
    {
        if (what.Contains("dark")) { _state.Settings.Theme = "Dark"; ApplyTheme(); }
        var o = new DownloadOptions { Folder = AppState.DefaultFolder() };
        var now = DateTime.Now;
        _state.Items.AddRange(new[]
        {
            new DownloadItem { Title = "Mountain trail timelapse — sunrise to sunset", Uploader = "Sample Channel", DurationSeconds = 754, Options = o.Clone(), Status = ItemStatus.Downloading, Progress = .42, Speed = 3.4e6, Eta = 38, TotalBytes = 214_000_000, Added = now },
            new DownloadItem { Title = "Lo-fi beats for studying (1 hour mix)", Uploader = "Another Creator", DurationSeconds = 3612, Options = new DownloadOptions { Kind = MediaKind.Audio, Format = "mp3" }, Status = ItemStatus.Queued, Added = now.AddMinutes(-1) },
            new DownloadItem { Title = "How to bake sourdough bread at home", Uploader = "Kitchen Notes", DurationSeconds = 1265, Options = new DownloadOptions { MaxHeight = 2160, Subtitles = true }, Status = ItemStatus.Done, TotalBytes = 1_480_000_000, Added = now.AddMinutes(-5), Playlist = "Weekend baking" },
            new DownloadItem { Title = "Episode 12: Building a tiny cabin", Uploader = "Workshop", DurationSeconds = 932, Options = new DownloadOptions { MaxHeight = 720 }, Status = ItemStatus.Paused, Progress = .63, TotalBytes = 98_000_000, Added = now.AddMinutes(-7) },
            new DownloadItem { Title = "Private video", Options = o.Clone(), Status = ItemStatus.Failed, Message = "This video is private. Turn on 'Use sign-in from browser' in Settings.", Added = now.AddMinutes(-9) },
        });
        _banner.Visible = false;
        Changed();
        if (what.Contains("options"))
        {
            var info = new LinkInfo { Title = "How to bake sourdough bread at home", Uploader = "Kitchen Notes", Duration = 1265, Heights = new() { 2160, 1440, 1080, 720, 480, 360 }, SizeByHeight = new() { [2160] = 1_400_000_000, [1080] = 310_000_000, [720] = 150_000_000 }, AudioSize = 20_000_000, SubtitleLangs = new() { "en", "es", "fr", "de" } };
            _previewForm = new OptionsDialog(info, o, "Download");
        }
        else if (what.Contains("playlist"))
        {
            var info = new LinkInfo { Title = "Weekend baking", Uploader = "Kitchen Notes", IsPlaylist = true };
            for (int i = 1; i <= 12; i++) info.Entries.Add(new PlaylistEntry { Title = $"Baking lesson {i}", Duration = 300 + i * 41, Url = "x" + i, Id = "x" + i });
            _previewForm = new OptionsDialog(info, o, "Download playlist");
        }
        else if (what.Contains("settings")) _previewForm = new SettingsDialog(_state.Settings);
        else if (what.Contains("subs")) Navigate("subs");
        if (_previewForm != null) { _previewForm.StartPosition = FormStartPosition.Manual; _previewForm.Location = new Point(Left + 40, Top + 40); _previewForm.Show(); }
    }

    static bool IsBlank(Bitmap b)
    {
        var c0 = b.GetPixel(b.Width / 2, b.Height / 2);
        for (int i = 0; i < 64; i++)
        {
            var c = b.GetPixel((i * 37) % b.Width, (i * 53) % b.Height);
            if (c != c0) return false;
        }
        return true;
    }

    // ---------------- navigation ----------------

    void Navigate(string key)
    {
        _nav.SelectedKey = key;
        bool subs = key == "subs";
        _subs.Visible = subs;
        _list.Visible = !subs;
        _list.Filter = key switch
        {
            "active" => i => i.Status is ItemStatus.Downloading or ItemStatus.Queued or ItemStatus.Paused,
            "done" => i => i.Status == ItemStatus.Done,
            "failed" => i => i.Status is ItemStatus.Failed or ItemStatus.Canceled,
            "video" => i => i.Options.Kind == MediaKind.Video && !i.Direct,
            "audio" => i => i.Options.Kind == MediaKind.Audio,
            "playlists" => i => i.Playlist.Length > 0,
            _ => _ => true,
        };
        _list.EmptyTitle = key switch
        {
            "active" => "Nothing downloading right now",
            "done" => "No finished downloads yet",
            "failed" => "Everything went fine",
            "playlists" => "No playlist downloads yet",
            _ => "Copy a video link, then press Paste Link",
        };
        _list.Rebuild();
        _nav.Invalidate();
        if (subs) _subs.Rebuild();
    }

    void UpdateCounts()
    {
        var items = _state.Items;
        foreach (var n in _nav.Items)
        {
            n.Count = n.Key switch
            {
                "all" => items.Count,
                "active" => items.Count(i => i.Status is ItemStatus.Downloading or ItemStatus.Queued or ItemStatus.Paused),
                "done" => items.Count(i => i.Status == ItemStatus.Done),
                "failed" => items.Count(i => i.Status is ItemStatus.Failed),
                "subs" => _state.Subscriptions.Count,
                _ => -1,
            };
        }
        _nav.Invalidate();
    }

    // ---------------- adding links ----------------

    static readonly Regex UrlRx = new(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);

    async Task PasteFromClipboard()
    {
        string text = "";
        try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
        if (UrlRx.Matches(text).Count == 0)
        {
            using var p = new PromptDialog("Paste a link", "Copy a link from your browser, or type it here:", text.Trim());
            if (p.ShowDialog(this) != DialogResult.OK) return;
            text = p.Value;
        }
        await AddLinks(text);
    }

    public async Task AddLinks(string text)
    {
        var urls = UrlRx.Matches(text).Select(m => m.Value.TrimEnd('.', ',', ')')).Distinct().ToList();
        if (urls.Count == 0)
        {
            ShowBanner("That doesn't look like a link. Copy the address of a video page and try again.", null, null, true);
            return;
        }
        if (urls.Count > 1 && !_state.Settings.SmartMode)
        {
            // several links: one choice for all
            using var d = new OptionsDialog(null, _state.Settings.LastOptions, $"Download {urls.Count} links");
            if (d.ShowDialog(this) != DialogResult.OK) return;
            _state.Settings.LastOptions = d.Result.Clone();
            foreach (var u in urls) await AnalyzeAndQueue(u, d.Result, quiet: true);
            return;
        }
        foreach (var u in urls)
            await AnalyzeAndQueue(u, _state.Settings.SmartMode ? _state.Settings.SmartOptions : null, quiet: _state.Settings.SmartMode);
    }

    async Task AnalyzeAndQueue(string url, DownloadOptions? preset, bool quiet)
    {
        _analyzeCts?.Cancel();
        var cts = _analyzeCts = new CancellationTokenSource();
        ShowBanner("Looking at the link…  " + Shorten(url), null, null);
        LinkInfo info;
        try { info = await Engine.AnalyzeAsync(url, cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (InvalidOperationException ex) when (ex.Message == "ENGINE_MISSING")
        {
            ShowBanner("To download from video sites, install the free download engine first (one time, about 140 MB).", "Install now", () => _ = InstallEngine(url), true);
            return;
        }
        catch (Exception ex)
        {
            ShowBanner("Couldn't read that link: " + ex.Message, "Try again", () => _ = AnalyzeAndQueue(url, preset, quiet), true);
            return;
        }
        if (cts.IsCancellationRequested) return;
        _banner.Visible = false;

        DownloadOptions opts;
        List<PlaylistEntry> entries;
        if (preset != null && quiet)
        {
            opts = preset.Clone();
            entries = info.Entries;
        }
        else
        {
            using var d = new OptionsDialog(info, preset ?? _state.Settings.LastOptions, info.IsPlaylist ? "Download playlist" : "Download");
            if (d.ShowDialog(this) != DialogResult.OK) return;
            opts = d.Result.Clone();
            entries = d.ChosenEntries;
            _state.Settings.LastOptions = opts.Clone();
        }
        Queue(info, opts, entries);
    }

    public void Queue(LinkInfo info, DownloadOptions opts, List<PlaylistEntry> entries, string? playlistName = null)
    {
        if (info.IsPlaylist)
        {
            var name = playlistName ?? (info.Title.Length > 0 ? info.Title : "Playlist");
            var t = DateTime.Now;
            int k = 0;
            foreach (var e in entries)
            {
                _state.Items.Add(new DownloadItem
                {
                    Url = e.Url, Title = e.Title, Uploader = e.Uploader.Length > 0 ? e.Uploader : info.Uploader, ThumbnailUrl = e.Thumbnail,
                    DurationSeconds = e.Duration, Playlist = name, Options = opts.Clone(), Added = t.AddMilliseconds(-(k++)),
                });
            }
            if (_shot == null && _state.Settings.SmartMode) ShowBanner($"Added {entries.Count} videos from “{name}”.", null, null);
        }
        else
        {
            _state.Items.Add(new DownloadItem
            {
                Url = info.Url, Title = info.Title, Uploader = info.Uploader, ThumbnailUrl = info.Thumbnail, DurationSeconds = info.Duration,
                Options = opts.Clone(), Direct = info.IsDirectFile, TotalBytes = info.DirectSize,
            });
        }
        if (_nav.SelectedKey is "done" or "failed" or "subs") Navigate("all");
        Changed();
    }

    async Task InstallEngine(string? retryUrl)
    {
        _bannerBtn.Visible = false;
        var prog = new Progress<(string text, double frac)>(p => { _bannerText.Text = "Installing download engine — " + p.text + (p.frac > 0 && p.frac < 1 ? $"  ({p.frac * 100:0}%)" : ""); });
        try
        {
            await Engine.InstallEngineAsync(prog, true, CancellationToken.None);
            ShowBanner("Download engine installed. You're all set.", null, null);
            if (retryUrl != null) await AnalyzeAndQueue(retryUrl, _state.Settings.SmartMode ? _state.Settings.SmartOptions : null, _state.Settings.SmartMode);
        }
        catch (Exception ex)
        {
            ShowBanner("Couldn't install the engine: " + ex.Message, "Try again", () => _ = InstallEngine(retryUrl), true);
        }
    }

    void CheckEngineBanner()
    {
        if (!Engine.HasEngine)
            ShowBanner("One-time setup: install the free download engine to save videos from YouTube and 1,000+ other sites. Direct file links work already.", "Install engine", () => _ = InstallEngine(null), true);
    }

    static string Shorten(string u) => u.Length > 70 ? u[..67] + "…" : u;

    void ToggleSmart()
    {
        if (_smart.Checked)
        {
            using var d = new OptionsDialog(null, _state.Settings.SmartOptions, "Smart Mode choices");
            if (d.ShowDialog(this) != DialogResult.OK) { _smart.Checked = false; _smart.Invalidate(); return; }
            _state.Settings.SmartOptions = d.Result.Clone();
            _state.Settings.SmartMode = true;
            ShowBanner($"Smart Mode is on: links download right away as {_state.Settings.SmartOptions.Describe()}.", "Change", () => { _smart.Checked = true; ToggleSmart(); });
        }
        else
        {
            _state.Settings.SmartMode = false;
            _banner.Visible = false;
        }
        Changed();
    }

    void OpenSettings()
    {
        using var d = new SettingsDialog(_state.Settings);
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            ApplyTheme();
            if (Engine.HasEngine && _banner.Visible && _bannerText.Text.StartsWith("One-time")) _banner.Visible = false;
            Changed();
        }
        else if (Engine.HasEngine && _banner.Visible && _bannerText.Text.StartsWith("One-time")) _banner.Visible = false;
    }

    // ---------------- row actions ----------------

    void OnRowAction(DownloadItem it, RowAction a)
    {
        switch (a)
        {
            case RowAction.Primary:
                if (it.Status is ItemStatus.Downloading or ItemStatus.Queued) Pause(it);
                else if (it.Status == ItemStatus.Done) OpenFile(it);
                else { it.Status = ItemStatus.Queued; it.Message = ""; }
                break;
            case RowAction.Folder: ShowInFolder(it); break;
            case RowAction.Remove: Remove(new[] { it }, false); break;
        }
        Changed();
    }

    void Pause(DownloadItem it)
    {
        if (_running.TryGetValue(it.Id, out var c)) c.Cancel();
        it.Status = ItemStatus.Paused;
    }

    void Remove(IEnumerable<DownloadItem> items, bool deleteFiles)
    {
        var list = items.ToList();
        if (deleteFiles && MessageBox.Show(this, $"Delete {list.Count} file(s) from your computer?", "Delete files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var it in list)
        {
            if (_running.TryGetValue(it.Id, out var c)) c.Cancel();
            if (deleteFiles && File.Exists(it.FilePath)) try { File.Delete(it.FilePath); } catch { }
            _state.Items.Remove(it);
        }
        Changed();
    }

    void OpenFile(DownloadItem it)
    {
        if (File.Exists(it.FilePath)) Process.Start(new ProcessStartInfo(it.FilePath) { UseShellExecute = true });
        else ShowInFolder(it);
    }

    void ShowInFolder(DownloadItem it)
    {
        if (File.Exists(it.FilePath)) Process.Start("explorer.exe", $"/select,\"{it.FilePath}\"");
        else
        {
            var f = it.Options.Folder;
            if (it.Options.PlaylistSubfolder && it.Playlist.Length > 0) { var sub = Path.Combine(f, Engine.SafeName(it.Playlist)); if (Directory.Exists(sub)) f = sub; }
            if (Directory.Exists(f)) Process.Start("explorer.exe", $"\"{f}\"");
        }
    }

    ContextMenuStrip BuildMenu()
    {
        var sel = _list.Selected.ToList();
        var m = new ContextMenuStrip { Font = Theme.Base, ShowImageMargin = false };
        if (Theme.Dark) { m.BackColor = Theme.Surface; m.ForeColor = Theme.Text; m.Renderer = new ToolStripProfessionalRenderer(new DarkColors()); }
        if (sel.Count == 0) return m;
        var one = sel.Count == 1 ? sel[0] : null;
        if (one?.Status == ItemStatus.Done) m.Items.Add("Play", null, (_, _) => OpenFile(one));
        if (one != null) m.Items.Add("Show in folder", null, (_, _) => ShowInFolder(one));
        if (one != null) m.Items.Add("Copy link", null, (_, _) => { try { Clipboard.SetText(one.Url); } catch { } });
        if (one != null) m.Items.Add("Open page in browser", null, (_, _) => Process.Start(new ProcessStartInfo(one.Url) { UseShellExecute = true }));
        m.Items.Add(new ToolStripSeparator());
        if (sel.Any(i => i.Status is ItemStatus.Downloading or ItemStatus.Queued))
            m.Items.Add("Pause", null, (_, _) => { foreach (var i in sel) if (i.Status is ItemStatus.Downloading or ItemStatus.Queued) Pause(i); Changed(); });
        if (sel.Any(i => i.Status is ItemStatus.Paused or ItemStatus.Failed or ItemStatus.Canceled))
            m.Items.Add("Resume / try again", null, (_, _) => { foreach (var i in sel) if (i.Status is ItemStatus.Paused or ItemStatus.Failed or ItemStatus.Canceled) { i.Status = ItemStatus.Queued; i.Message = ""; } Changed(); });
        if (sel.Any(i => i.Status == ItemStatus.Done))
            m.Items.Add("Download again", null, (_, _) => { foreach (var i in sel.Where(i => i.Status == ItemStatus.Done)) { i.Status = ItemStatus.Queued; i.FilePath = ""; i.Progress = 0; } Changed(); });
        if (one != null && !one.Direct)
            m.Items.Add("Change quality or format…", null, (_, _) =>
            {
                using var d = new OptionsDialog(null, one.Options, "Choices for this download");
                if (d.ShowDialog(this) == DialogResult.OK) { if (_running.TryGetValue(one.Id, out var c)) c.Cancel(); one.Options = d.Result.Clone(); one.Status = ItemStatus.Queued; one.Progress = 0; Changed(); }
            });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Remove from list", null, (_, _) => Remove(sel, false));
        if (sel.Any(i => File.Exists(i.FilePath))) m.Items.Add("Remove and delete file", null, (_, _) => Remove(sel, true));
        return m;
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemBorder => Theme.Border;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V && ActiveControl is not TextBox) { _ = PasteFromClipboard(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.F) { _search.Focus(); e.Handled = true; }
        else if (e.KeyCode == Keys.Delete && ActiveControl == _list) { Remove(_list.Selected, false); e.Handled = true; }
        else if (e.KeyCode == Keys.Enter && ActiveControl == _list) { var one = _list.Selected.FirstOrDefault(); if (one?.Status == ItemStatus.Done) OpenFile(one); }
    }

    // ---------------- scheduler ----------------

    public void Changed() { _dirty = true; _list.Rebuild(); UpdateCounts(); if (_subs.Visible) _subs.Rebuild(); }

    void Tick()
    {
        int max = Math.Max(1, _state.Settings.MaxConcurrent);
        int running = _running.Count;
        foreach (var it in _state.Items.Where(i => i.Status == ItemStatus.Queued).OrderBy(i => i.Added).ToList())
        {
            if (running >= max || _shot != null) break;
            Start(it);
            running++;
        }
        var active = _state.Items.Where(i => i.Status == ItemStatus.Downloading).ToList();
        double speed = active.Sum(i => i.Speed);
        int queued = _state.Items.Count(i => i.Status == ItemStatus.Queued);
        var parts = new List<string>();
        if (active.Count > 0) parts.Add($"{active.Count} downloading" + (speed > 0 ? $" at {Fmt.Bytes(speed)}/s" : ""));
        if (queued > 0) parts.Add($"{queued} waiting");
        if (parts.Count == 0) parts.Add(_state.Items.Count == 0 ? "Ready" : $"{_state.Items.Count(i => i.Status == ItemStatus.Done)} of {_state.Items.Count} downloaded");
        if (!Engine.HasEngine) parts.Add("engine not installed");
        else if (!Engine.HasFfmpeg) parts.Add("converter missing (Settings)");
        _statusText.Text = string.Join("   ·   ", parts);
        if (active.Count > 0) _list.Invalidate();
        if (_dirty && _shot == null) { _dirty = false; var st = _state; _ = Task.Run(st.Save); }
    }

    void Start(DownloadItem it)
    {
        var cts = new CancellationTokenSource();
        _running[it.Id] = cts;
        it.Status = ItemStatus.Downloading;
        it.Message = ""; it.Speed = 0; it.Eta = -1; it.Phase = "";
        Changed();
        _ = Task.Run(async () =>
        {
            string? error = null; bool canceled = false;
            try { await Engine.RunDownloadAsync(it, cts.Token); }
            catch (OperationCanceledException) { canceled = true; }
            catch (Exception ex) { if (cts.IsCancellationRequested) canceled = true; else error = ex.Message; }
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _running.Remove(it.Id);
                if (canceled) { if (it.Status == ItemStatus.Downloading) it.Status = ItemStatus.Paused; }
                else if (error != null) { it.Status = ItemStatus.Failed; it.Message = error; }
                else
                {
                    it.Status = ItemStatus.Done; it.Progress = 1; it.Speed = 0; it.Added = it.Added; it.Phase = "";
                    NotifyDone(it);
                }
                Changed();
            });
        });
    }

    void NotifyDone(DownloadItem it)
    {
        if (!_state.Settings.NotifyWhenDone || ContainsFocus) return;
        if (_state.Items.Any(i => i.Status is ItemStatus.Downloading or ItemStatus.Queued)) return;
        _tray.Visible = true;
        _tray.ShowBalloonTip(4000, "Downloads finished", it.Title, ToolTipIcon.Info);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_shot == null && _running.Count > 0 &&
            MessageBox.Show(this, "Downloads are still running. They'll pause and you can resume them next time. Quit now?", "ClipHarbor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        { e.Cancel = true; return; }
        foreach (var c in _running.Values) c.Cancel();
        foreach (var it in _state.Items.Where(i => i.Status == ItemStatus.Downloading)) it.Status = ItemStatus.Paused;
        if (_shot == null) _state.Save();
        _tray.Visible = false;
        base.OnFormClosing(e);
    }

    // ---------------- art ----------------

    public static void DrawLogo(Graphics g, RectangleF r)
    {
        using var br = new LinearGradientBrush(r, Color.FromArgb(0x2F, 0x6F, 0xEB), Color.FromArgb(0x7A, 0x4C, 0xE6), 45f);
        using var p = Theme.Round(r, r.Width * 0.26f);
        g.FillPath(br, p);
        float cx = r.X + r.Width / 2, w = r.Width;
        using var pen = new Pen(Color.White, w * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, cx, r.Y + w * 0.22f, cx, r.Y + w * 0.62f);
        g.DrawLines(pen, new[] { new PointF(cx - w * 0.18f, r.Y + w * 0.45f), new PointF(cx, r.Y + w * 0.63f), new PointF(cx + w * 0.18f, r.Y + w * 0.45f) });
        g.DrawLine(pen, r.X + w * 0.25f, r.Y + w * 0.78f, r.Right - w * 0.25f, r.Y + w * 0.78f);
    }

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; DrawLogo(g, new RectangleF(2, 2, 60, 60)); }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
