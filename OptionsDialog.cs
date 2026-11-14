namespace ClipHarbor;

/// <summary>Choose what and how to download. Also used (without a link) to set Smart Mode / subscription choices.</summary>
public class OptionsDialog : Form
{
    readonly LinkInfo? _info;
    public DownloadOptions Result;
    public List<PlaylistEntry> ChosenEntries = new();

    readonly RadioButton _video = new() { Text = "Video", AutoSize = true, Checked = true };
    readonly RadioButton _audio = new() { Text = "Audio only", AutoSize = true };
    readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly Label _qualityLabel = new() { Text = "Quality", AutoSize = true };
    readonly CheckBox _subs = new() { Text = "Download subtitles", AutoSize = true };
    readonly CheckBox _embed = new() { Text = "Put them inside the video", AutoSize = true, Checked = true };
    readonly CheckedListBox _subLangs = new() { CheckOnClick = true, Height = 92, Width = 270, IntegralHeight = false };
    readonly TextBox _subText = new() { Width = 270, PlaceholderText = "Languages, e.g. en, es, fr" };
    readonly TextBox _folder = new() { Width = 420 };
    readonly CheckBox _subfolder = new() { Text = "Put playlist videos in their own folder", AutoSize = true, Checked = true };
    readonly CheckedListBox _entries = new() { CheckOnClick = true, IntegralHeight = false };
    readonly List<int> _qualityValues = new();

    static readonly string[] VideoFormats = { "mp4", "mkv", "webm" };
    static readonly string[] AudioFormats = { "mp3", "m4a", "opus", "flac", "wav" };

    public OptionsDialog(LinkInfo? info, DownloadOptions start, string heading)
    {
        _info = info;
        Result = start.Clone();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = heading;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        KeyPreview = true;
        Font = Theme.Base;

        int y = 18, L = 22, W = 600;
        bool playlist = info?.IsPlaylist == true;

        // header
        var header = new HeaderPanel(info, heading) { Location = new Point(L, y), Size = new Size(W, 86) };
        Controls.Add(header);
        y += 100;

        if (playlist)
        {
            _entries.Location = new Point(L, y + 26);
            _entries.Size = new Size(W, 170);
            foreach (var e in info!.Entries)
                _entries.Items.Add(EntryText(e), true);
            var lbl = new Label { Text = $"Videos in this playlist ({info.Entries.Count})", AutoSize = true, Location = new Point(L, y + 2), Font = Theme.Bold };
            var all = new LinkLabel { Text = "Select all", AutoSize = true, Location = new Point(L + W - 150, y + 2), LinkColor = Theme.Accent };
            var none = new LinkLabel { Text = "None", AutoSize = true, Location = new Point(L + W - 60, y + 2), LinkColor = Theme.Accent };
            all.Click += (_, _) => { for (int i = 0; i < _entries.Items.Count; i++) _entries.SetItemChecked(i, true); };
            none.Click += (_, _) => { for (int i = 0; i < _entries.Items.Count; i++) _entries.SetItemChecked(i, false); };
            Controls.AddRange(new Control[] { lbl, all, none, _entries });
            y += 210;
        }

        Controls.Add(new Label { Text = "Download as", AutoSize = true, Location = new Point(L, y + 4), Font = Theme.Bold });
        _video.Location = new Point(L + 130, y + 2);
        _audio.Location = new Point(L + 220, y + 2);
        Controls.AddRange(new Control[] { _video, _audio });
        y += 38;

        Controls.Add(new Label { Text = "Format", AutoSize = true, Location = new Point(L, y + 4) });
        _format.Location = new Point(L + 130, y);
        _qualityLabel.Location = new Point(L + 300, y + 4);
        _quality.Location = new Point(L + 360, y);
        _quality.Width = W - 360;
        Controls.AddRange(new Control[] { _format, _qualityLabel, _quality });
        y += 40;

        _subs.Location = new Point(L, y);
        _embed.Location = new Point(L + 300, y);
        Controls.AddRange(new Control[] { _subs, _embed });
        y += 30;
        bool listLangs = info != null && !playlist && !info.IsDirectFile && (info.SubtitleLangs.Count + info.AutoSubtitleLangs.Count) > 0;
        if (listLangs)
        {
            _subLangs.Location = new Point(L + 20, y);
            _subLangs.Width = W - 20;
            foreach (var l in info!.SubtitleLangs) _subLangs.Items.Add(l, Result.SubtitleLanguages.Contains(l));
            foreach (var l in info.AutoSubtitleLangs.Where(l => !info.SubtitleLangs.Contains(l))) _subLangs.Items.Add(l + " (auto)", false);
            if (_subLangs.CheckedItems.Count == 0 && _subLangs.Items.Count > 0)
            {
                int en = Enumerable.Range(0, _subLangs.Items.Count).FirstOrDefault(i => _subLangs.Items[i]!.ToString()!.StartsWith("en"), 0);
                _subLangs.SetItemChecked(en, true);
            }
            Controls.Add(_subLangs);
            y += 100;
        }
        else
        {
            _subText.Location = new Point(L + 20, y);
            _subText.Text = string.Join(", ", Result.SubtitleLanguages);
            Controls.Add(_subText);
            var hint = new Label { Text = info == null || playlist ? "Used whenever the video has them" : "No subtitles were listed for this video", Tag = "sub", AutoSize = true, Location = new Point(L + 300, y + 3) };
            Controls.Add(hint);
            y += 36;
        }

        Controls.Add(new Label { Text = "Save to", AutoSize = true, Location = new Point(L, y + 4), Font = Theme.Bold });
        _folder.Location = new Point(L + 130, y);
        _folder.Width = W - 130 - 100;
        _folder.Text = Result.Folder;
        var browse = new FlatButton { Text = "Browse…", Location = new Point(L + W - 90, y - 3), Size = new Size(90, 30) };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _folder.Text, UseDescriptionForTitle = true, Description = "Choose where downloads are saved" };
            if (d.ShowDialog(this) == DialogResult.OK) _folder.Text = d.SelectedPath;
        };
        Controls.AddRange(new Control[] { _folder, browse });
        y += 38;

        if (playlist || info == null)
        {
            _subfolder.Location = new Point(L + 130, y);
            _subfolder.Checked = Result.PlaylistSubfolder;
            Controls.Add(_subfolder);
            y += 32;
        }

        y += 10;
        var ok = new FlatButton { Text = info == null ? "Save" : "Download", Primary = true, Glyph = info == null ? "" : "", Size = new Size(140, 36), Location = new Point(L + W - 140, y) };
        var cancel = new FlatButton { Text = "Cancel", Size = new Size(100, 36), Location = new Point(L + W - 250, y) };
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.AddRange(new Control[] { ok, cancel });
        if (!Engine.HasFfmpeg && info?.IsDirectFile != true)
            Controls.Add(new Label
            {
                Text = "Tip: install the converter in Settings for 1080p and above,\nMP3 and subtitles inside the video.",
                Tag = "sub", AutoSize = true, Location = new Point(L, y + 1)
            });
        if (info?.IsDirectFile == true)
        {
            foreach (Control c in new Control[] { _video, _audio, _format, _quality, _qualityLabel, _subs, _embed, _subText }) c.Enabled = false;
        }
        ClientSize = new Size(W + L * 2, y + 36 + 20);

        _video.CheckedChanged += (_, _) => KindChanged();
        _subs.CheckedChanged += (_, _) => SubsChanged();
        _format.SelectedIndexChanged += (_, _) => FillQuality();
        if (Result.Kind == MediaKind.Audio) _audio.Checked = true;
        _subs.Checked = Result.Subtitles;
        _embed.Checked = Result.EmbedSubtitles;
        KindChanged();
        SubsChanged();

        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } if (e.KeyCode == Keys.Enter && ActiveControl is not CheckedListBox) Accept(); };
        Theme.Style(this);
        AcceptButton = null;
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.TitleBar(this); }

    static string EntryText(PlaylistEntry e) => e.Duration > 0 ? $"{e.Title}   ({Fmt.Duration(e.Duration)})" : e.Title;

    void KindChanged()
    {
        bool video = _video.Checked;
        var list = video ? VideoFormats : AudioFormats;
        _format.Items.Clear();
        foreach (var f in list) _format.Items.Add(f.ToUpperInvariant());
        int idx = Array.IndexOf(list, Result.Format);
        _format.SelectedIndex = idx >= 0 ? idx : 0;
        _quality.Visible = _qualityLabel.Visible = video;
        _subs.Enabled = video && _info?.IsDirectFile != true;
        SubsChanged();
        FillQuality();
    }

    void SubsChanged()
    {
        bool on = _subs.Checked && _subs.Enabled;
        _embed.Enabled = on && Engine.HasFfmpeg;
        _subLangs.Enabled = on;
        _subText.Enabled = on;
    }

    void FillQuality()
    {
        if (!_video.Checked) return;
        _quality.Items.Clear(); _qualityValues.Clear();
        var heights = _info?.Heights.Count > 0
            ? _info.Heights.Where(h => h >= 144).Select(h => h >= 4320 ? 4320 : h >= 2160 ? 2160 : h >= 1440 ? 1440 : h >= 1080 ? 1080 : h >= 720 ? 720 : h >= 480 ? 480 : h >= 360 ? 360 : h >= 240 ? 240 : 144).Distinct().ToList()
            : new List<int> { 4320, 2160, 1440, 1080, 720, 480, 360 };
        _quality.Items.Add("Best available"); _qualityValues.Add(0);
        foreach (var h in heights)
        {
            string size = "";
            if (_info != null)
            {
                var best = _info.SizeByHeight.Where(kv => kv.Key <= h).OrderByDescending(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault();
                if (best > 0) size = "   ~" + Fmt.Bytes(best + _info.AudioSize);
            }
            _quality.Items.Add(DownloadOptions.QualityLabel(h) + (h >= 720 && h < 1440 ? " HD" : "") + size);
            _qualityValues.Add(h);
        }
        int want = Result.MaxHeight;
        int sel = _qualityValues.IndexOf(want);
        if (sel < 0 && want > 0) sel = _qualityValues.FindIndex(v => v > 0 && v <= want);
        _quality.SelectedIndex = sel >= 0 ? sel : 0;
    }

    void Accept()
    {
        var o = Result;
        o.Kind = _audio.Checked ? MediaKind.Audio : MediaKind.Video;
        o.Format = (_format.SelectedItem?.ToString() ?? "mp4").ToLowerInvariant();
        if (o.Kind == MediaKind.Video && _quality.SelectedIndex >= 0) o.MaxHeight = _qualityValues[_quality.SelectedIndex];
        o.Subtitles = _subs.Checked && _subs.Enabled;
        o.EmbedSubtitles = _embed.Checked;
        if (_subLangs.Parent != null)
        {
            var langs = _subLangs.CheckedItems.Cast<object>().Select(x => x.ToString()!.Replace(" (auto)", "")).ToList();
            if (langs.Count > 0) o.SubtitleLanguages = langs;
        }
        else
        {
            var langs = _subText.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (langs.Count > 0) o.SubtitleLanguages = langs;
        }
        var folder = _folder.Text.Trim();
        if (folder.Length == 0) folder = AppState.DefaultFolder();
        try { Directory.CreateDirectory(folder); }
        catch { MessageBox.Show(this, "That folder can't be used. Please pick another one.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        o.Folder = folder;
        o.PlaylistSubfolder = _subfolder.Checked;

        if (_info?.IsPlaylist == true)
        {
            ChosenEntries = _entries.CheckedIndices.Cast<int>().Select(i => _info.Entries[i]).ToList();
            if (ChosenEntries.Count == 0) { MessageBox.Show(this, "Pick at least one video from the playlist.", Text); return; }
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    class HeaderPanel : Control
    {
        readonly LinkInfo? _i; readonly string _heading;
        public HeaderPanel(LinkInfo? i, string heading)
        {
            _i = i; _heading = heading;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Thumbs.Loaded += () => { if (IsHandleCreated) BeginInvoke(Invalidate); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float s = DeviceDpi / 96f;
            g.Clear(Theme.Surface);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var th = new RectangleF(0, 0, Height * 16f / 9f, Height);
            var img = _i == null ? null : Thumbs.Get(_i.Thumbnail);
            if (img != null) { using var clip = Theme.Round(th, 8 * s); g.SetClip(clip); g.DrawImage(img, th); g.ResetClip(); }
            else Thumbs.DrawPlaceholder(g, th, false, s);
            int x = (int)(th.Right + 16 * s);
            string title = _i?.Title ?? _heading;
            TextRenderer.DrawText(g, title, Theme.Title, new Rectangle(x, (int)(4 * s), Width - x, (int)(50 * s)), Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            var bits = new List<string>();
            if (_i == null) bits.Add("These choices are used every time");
            else if (_i.IsPlaylist) bits.Add($"Playlist · {_i.Entries.Count} videos");
            else if (_i.IsDirectFile) { bits.Add("Direct file link"); if (_i.DirectSize > 0) bits.Add(Fmt.Bytes(_i.DirectSize)); }
            if (_i != null && _i.Uploader.Length > 0) bits.Add(_i.Uploader);
            if (_i != null && _i.Duration > 0) bits.Add(Fmt.Duration(_i.Duration));
            TextRenderer.DrawText(g, string.Join("  ·  ", bits), Theme.Base, new Rectangle(x, Height - (int)(24 * s), Width - x, (int)(22 * s)), Theme.SubText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
