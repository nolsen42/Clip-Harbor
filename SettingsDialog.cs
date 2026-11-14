namespace ClipHarbor;

public class SettingsDialog : Form
{
    readonly AppSettings _s;
    readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    readonly NumericUpDown _parallel = new() { Minimum = 1, Maximum = 32, Width = 70 };
    readonly TextBox _template = new() { Width = 300 };
    readonly CheckBox _skip = new() { Text = "Skip files that are already downloaded", AutoSize = true };
    readonly CheckBox _notify = new() { Text = "Show a notice when downloads finish", AutoSize = true };
    readonly TextBox _limit = new() { Width = 120, PlaceholderText = "No limit" };
    readonly TextBox _proxy = new() { Width = 300, PlaceholderText = "e.g. http://127.0.0.1:8080" };
    readonly ComboBox _cookies = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    readonly CheckBox _thumb = new() { Text = "Add the cover picture to files", AutoSize = true };
    readonly CheckBox _meta = new() { Text = "Add title, artist and date to files", AutoSize = true };
    readonly NumericUpDown _subHours = new() { Minimum = 0, Maximum = 168, Width = 70 };
    readonly Label _engineStatus = new() { AutoSize = true, MaximumSize = new Size(680, 0) };
    readonly ProgressBar _engineBar = new() { Width = 300, Height = 8, Visible = false };
    readonly TextBox _enginePath = new() { Width = 450, PlaceholderText = "Found automatically" };
    readonly TextBox _ffPath = new() { Width = 450, PlaceholderText = "Found automatically" };
    readonly FlatButton _install = new() { Text = "Install engine", Primary = true, Size = new Size(150, 32) };
    readonly FlatButton _update = new() { Text = "Check for updates", Size = new Size(160, 32) };
    CancellationTokenSource? _cts;

    static readonly string[] Browsers = { "", "chrome", "edge", "firefox", "brave", "opera", "vivaldi", "chromium" };

    public SettingsDialog(AppSettings s)
    {
        _s = s;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Font = Theme.Base;
        AutoScroll = true;

        int L = 24, C = 230, y = 16;
        void Section(string t) { y += 8; Controls.Add(new Label { Text = t, Font = Theme.Title, AutoSize = true, Location = new Point(L, y) }); y += 36; }
        void Row(string label, Control c, string? hint = null)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(L, y + 4) });
            c.Location = new Point(C, y);
            Controls.Add(c);
            if (hint != null) Controls.Add(new Label { Text = hint, Tag = "sub", AutoSize = true, Location = new Point(C + c.Width + 10, y + 4) });
            y += 36;
        }
        void Check(CheckBox c) { c.Location = new Point(C, y); Controls.Add(c); y += 30; }

        Section("General");
        _theme.Items.AddRange(new object[] { "Light", "Dark", "System" });
        _theme.SelectedItem = s.Theme;
        if (_theme.SelectedIndex < 0) _theme.SelectedIndex = 0;
        Row("Look", _theme);
        _parallel.Value = Math.Clamp(s.MaxConcurrent, 1, 32);
        Row("Downloads at the same time", _parallel, "no cap");
        _template.Text = s.FileNameTemplate;
        Row("File names", _template);
        Controls.Add(new Label { Text = "Codes: %(title)s  %(uploader)s  %(upload_date)s  %(ext)s", Tag = "sub", AutoSize = true, Location = new Point(C, y - 8) });
        y += 18;
        _skip.Checked = s.SkipExisting; Check(_skip);
        _notify.Checked = s.NotifyWhenDone; Check(_notify);

        Section("Downloading");
        _limit.Text = s.SpeedLimit;
        Row("Speed limit", _limit, "e.g. 500K or 2M per second");
        _proxy.Text = s.Proxy;
        Row("Proxy", _proxy);
        _cookies.Items.AddRange(new object[] { "Don't use", "Chrome", "Edge", "Firefox", "Brave", "Opera", "Vivaldi", "Chromium" });
        Row("Use sign-in from browser", _cookies, "for private or age-restricted videos");
        _thumb.Checked = s.EmbedThumbnail; Check(_thumb);
        _meta.Checked = s.EmbedMetadata; Check(_meta);

        Section("Subscriptions");
        _subHours.Value = Math.Clamp(s.SubscriptionCheckHours, 0, 168);
        Row("Check for new videos every", _subHours, "hours (0 = only when I ask)");

        Section("Download engine");
        _engineStatus.Location = new Point(L, y);
        Controls.Add(_engineStatus);
        y += 46;
        _engineBar.Location = new Point(L, y - 8);
        Controls.Add(_engineBar);
        _install.Location = new Point(L, y + 4);
        _update.Location = new Point(L + 160, y + 4);
        Controls.AddRange(new Control[] { _install, _update });
        y += 50;
        _enginePath.Text = s.EnginePath; Row("Engine file (yt-dlp.exe)", _enginePath);
        _ffPath.Text = s.FfmpegPath; Row("Converter file (ffmpeg.exe)", _ffPath);
        _install.Click += async (_, _) => await Install();
        _update.Click += async (_, _) => await UpdateEngine();

        y += 6;
        var ok = new FlatButton { Text = "Save", Primary = true, Size = new Size(120, 36), Location = new Point(L + 680 - 120, y) };
        var cancel = new FlatButton { Text = "Cancel", Size = new Size(100, 36), Location = new Point(L + 680 - 230, y) };
        ok.Click += (_, _) => { Save(); DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.AddRange(new Control[] { ok, cancel });
        int h = y + 36 + 20;
        ClientSize = new Size(L * 2 + 700, Math.Min(h, 800));
        if (h > 800) AutoScrollMinSize = new Size(0, h);
        Theme.Style(this);
        Load += (_, _) =>
        {
            _cookies.SelectedIndex = Math.Max(0, Array.IndexOf(Browsers, s.CookiesBrowser ?? ""));
            _theme.SelectedItem = s.Theme;
            if (_theme.SelectedIndex < 0) _theme.SelectedIndex = 0;
            ActiveControl = null;
        };
        RefreshEngine();
        FormClosed += (_, _) => _cts?.Cancel();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.TitleBar(this); }

    async void RefreshEngine()
    {
        var y = Engine.YtDlp; var f = Engine.Ffmpeg;
        if (y == null)
        {
            _engineStatus.Text = "The download engine isn't installed yet. It's free and open source (yt-dlp, with ffmpeg for high quality and audio). Press Install to get it — about 140 MB.";
            _engineStatus.ForeColor = Theme.Warn;
            _install.Text = "Install engine"; _update.Enabled = false;
            return;
        }
        _update.Enabled = true;
        _install.Text = f == null ? "Add converter" : "Reinstall";
        _engineStatus.ForeColor = Theme.Text;
        _engineStatus.Text = "Engine ready" + (f == null ? " — converter (ffmpeg) missing: 1080p+ and MP3 need it." : " with converter.");
        var v = await Engine.EngineVersionAsync();
        if (v.Length > 0 && !IsDisposed) _engineStatus.Text += $"  Version {v}.";
    }

    async Task Install()
    {
        _cts = new CancellationTokenSource();
        _install.Enabled = _update.Enabled = false;
        _engineBar.Visible = true;
        var prog = new Progress<(string text, double frac)>(p =>
        {
            if (IsDisposed) return;
            _engineStatus.ForeColor = Theme.Text;
            _engineStatus.Text = p.text;
            _engineBar.Value = (int)Math.Clamp(p.frac * 100, 0, 100);
        });
        try { await Engine.InstallEngineAsync(prog, true, _cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { if (!IsDisposed) { _engineStatus.Text = "Couldn't install: " + ex.Message; _engineStatus.ForeColor = Theme.Bad; } }
        if (IsDisposed) return;
        _engineBar.Visible = false;
        _install.Enabled = true;
        RefreshEngine();
    }

    async Task UpdateEngine()
    {
        _update.Enabled = false;
        _engineStatus.Text = "Checking for a newer engine…";
        try { _engineStatus.Text = await Engine.UpdateEngineAsync(CancellationToken.None); }
        catch (Exception ex) { _engineStatus.Text = ex.Message; }
        if (!IsDisposed) _update.Enabled = true;
    }

    void Save()
    {
        _s.Theme = _theme.SelectedItem?.ToString() ?? "Light";
        _s.MaxConcurrent = (int)_parallel.Value;
        _s.FileNameTemplate = string.IsNullOrWhiteSpace(_template.Text) ? "%(title)s.%(ext)s" : _template.Text.Trim();
        _s.SkipExisting = _skip.Checked;
        _s.NotifyWhenDone = _notify.Checked;
        _s.SpeedLimit = _limit.Text.Trim();
        _s.Proxy = _proxy.Text.Trim();
        _s.CookiesBrowser = Browsers[Math.Max(0, _cookies.SelectedIndex)];
        _s.EmbedThumbnail = _thumb.Checked;
        _s.EmbedMetadata = _meta.Checked;
        _s.SubscriptionCheckHours = (int)_subHours.Value;
        _s.EnginePath = _enginePath.Text.Trim();
        _s.FfmpegPath = _ffPath.Text.Trim();
    }
}

