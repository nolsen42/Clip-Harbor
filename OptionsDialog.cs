using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace ClipHarbor;

/// <summary>Choose what and how to download. Also used (without a link) to set Smart Mode / subscription choices.</summary>
public class OptionsDialog : Dialog
{
    readonly LinkInfo? _info;
    public DownloadOptions Result;
    public List<PlaylistEntry> ChosenEntries = new();

    readonly RadioButton _video = new() { Content = "Video", GroupName = "kind", IsChecked = true };
    readonly RadioButton _audio = new() { Content = "Audio only", GroupName = "kind" };
    readonly ComboBox _format = new() { Width = 150 };
    readonly ComboBox _quality = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock _qualityLabel = new() { Text = "Quality", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 12, 0) };
    readonly CheckBox _subs = new() { Content = "Download subtitles" };
    readonly CheckBox _embed = new() { Content = "Put them inside the video", IsChecked = true };
    readonly WrapPanel _subLangs = new();
    readonly List<CheckBox> _langBoxes = new();
    readonly TextBox _subText = new() { Width = 270, Watermark = "Languages, e.g. en, es, fr" };
    readonly TextBox _folder = new();
    readonly CheckBox _subfolder = new() { Content = "Put playlist videos in their own folder", IsChecked = true };
    readonly ListBox _entries = new() { Height = 170, SelectionMode = SelectionMode.Multiple, Classes = { "compact" }, Background = Avalonia.Media.Brushes.Transparent };
    readonly List<int> _qualityValues = new();
    bool _useLangList;

    static readonly string[] VideoFormats = { "mp4", "mkv", "webm" };
    static readonly string[] AudioFormats = { "mp3", "m4a", "opus", "flac", "wav" };
    const double W = 600, LabelW = 130;

    public OptionsDialog(LinkInfo? info, DownloadOptions start, string heading) : base(heading)
    {
        _info = info;
        Result = start.Clone();
        bool playlist = info?.IsPlaylist == true;
        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 20), Spacing = 14, Width = W };

        root.Children.Add(Header(info, heading));

        if (playlist)
        {
            var head = new DockPanel();
            var all = new Button { Content = "Select all", Classes = { "link" } };
            var none = new Button { Content = "None", Classes = { "link" } };
            all.Click += (_, _) => SetAll(true);
            none.Click += (_, _) => SetAll(false);
            var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { all, none } };
            DockPanel.SetDock(links, Dock.Right);
            head.Children.Add(links);
            head.Children.Add(Ui.Text($"Videos in this playlist ({info!.Entries.Count})", "bold"));
            _entries.ItemTemplate = new FuncDataTemplate<PlaylistEntry>((e, _) =>
            {
                var cb = new CheckBox { Content = EntryText(e), IsChecked = e.Selected };
                cb.IsCheckedChanged += (_, _) => e.Selected = cb.IsChecked == true;
                return cb;
            }, supportsRecycling: false);
            _entries.ItemsSource = info.Entries;
            root.Children.Add(new StackPanel { Spacing = 6, Children = { head, new Border { Classes = { "panel" }, Child = _entries, ClipToBounds = true, CornerRadius = new CornerRadius(6) } } });
        }

        var kind = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18, Children = { _video, _audio } };
        root.Children.Add(Row("Download as", kind, bold: true));

        var formatRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        Grid.SetColumn(_qualityLabel, 1); Grid.SetColumn(_quality, 2);
        formatRow.Children.AddRange(new Control[] { _format, _qualityLabel, _quality });
        root.Children.Add(Row("Format", formatRow));

        var subsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Children = { _subs, _embed } };
        root.Children.Add(subsRow);
        _useLangList = info != null && !playlist && !info.IsDirectFile && (info.SubtitleLangs.Count + info.AutoSubtitleLangs.Count) > 0;
        if (_useLangList)
        {
            foreach (var l in info!.SubtitleLangs) AddLang(l, l, Result.SubtitleLanguages.Contains(l));
            foreach (var l in info.AutoSubtitleLangs.Where(l => !info.SubtitleLangs.Contains(l))) AddLang(l + " (auto)", l, false);
            if (!_langBoxes.Any(c => c.IsChecked == true) && _langBoxes.Count > 0)
                (_langBoxes.FirstOrDefault(c => ((string)c.Tag!).StartsWith("en")) ?? _langBoxes[0]).IsChecked = true;
            root.Children.Add(new Border
            {
                Classes = { "panel" }, CornerRadius = new CornerRadius(6), Margin = new Thickness(20, -6, 0, 0), Padding = new Thickness(10, 4), MaxHeight = 96,
                Child = new ScrollViewer { Content = _subLangs },
            });
        }
        else
        {
            _subText.Text = string.Join(", ", Result.SubtitleLanguages);
            var hint = Ui.Text(info == null || playlist ? "Used whenever the video has them" : "No subtitles were listed for this video", "sub");
            root.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(20, -6, 0, 0), Children = { _subText, hint } });
        }

        _folder.Text = Result.Folder;
        var browse = Ui.Button("Browse…");
        browse.FontWeight = FontWeight.Normal;
        browse.Click += async (_, _) =>
        {
            var start = await TryFolder(_folder.Text);
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose where downloads are saved", SuggestedStartLocation = start });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) _folder.Text = path;
        };
        var folderRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(browse, 1); browse.Margin = new Thickness(10, 0, 0, 0);
        folderRow.Children.AddRange(new Control[] { _folder, browse });
        root.Children.Add(Row("Save to", folderRow, bold: true));

        if (playlist || info == null)
        {
            _subfolder.IsChecked = Result.PlaylistSubfolder;
            _subfolder.Margin = new Thickness(LabelW, -6, 0, 0);
            root.Children.Add(_subfolder);
        }

        var ok = Ui.Button(info == null ? "Save" : "Download", info == null ? Icons.Check : Icons.Download, "accent");
        var cancel = Ui.Button("Cancel");
        ok.MinWidth = 140; cancel.MinWidth = 100;
        ok.HorizontalContentAlignment = cancel.HorizontalContentAlignment = HorizontalAlignment.Center;
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => Close(false);
        var bottom = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var btns = Buttons(cancel, ok);
        DockPanel.SetDock(btns, Dock.Right);
        bottom.Children.Add(btns);
        if (!Engine.HasFfmpeg && info?.IsDirectFile != true)
            bottom.Children.Add(new TextBlock { Text = "Tip: install the converter in Settings for 1080p and above,\nMP3 and subtitles inside the video.", Classes = { "sub", "small" }, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(bottom);

        if (info?.IsDirectFile == true)
            foreach (var c in new Control[] { _video, _audio, _format, _quality, _qualityLabel, _subs, _embed, _subText }) c.IsEnabled = false;

        Content = root;

        _video.IsCheckedChanged += (_, _) => KindChanged();
        _subs.IsCheckedChanged += (_, _) => SubsChanged();
        _format.SelectionChanged += (_, _) => FillQuality();
        if (Result.Kind == MediaKind.Audio) _audio.IsChecked = true;
        _subs.IsChecked = Result.Subtitles;
        _embed.IsChecked = Result.EmbedSubtitles;
        KindChanged();
        SubsChanged();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && FocusManager?.GetFocusedElement() is not (ListBoxItem or CheckBox or ComboBox)) { Accept(); e.Handled = true; }
        };
    }

    async Task<IStorageFolder?> TryFolder(string? path)
    {
        try { return string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? null : await StorageProvider.TryGetFolderFromPathAsync(path); }
        catch { return null; }
    }

    static Control Row(string label, Control content, bool bold = false)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelW},*") };
        var t = Ui.Text(label);
        if (bold) t.Classes.Add("bold");
        Grid.SetColumn(content, 1);
        g.Children.AddRange(new Control[] { t, content });
        return g;
    }

    void AddLang(string text, string code, bool on)
    {
        var cb = new CheckBox { Content = text, Tag = code, IsChecked = on, Margin = new Thickness(0, 0, 14, 0), MinWidth = 90 };
        _langBoxes.Add(cb);
        _subLangs.Children.Add(cb);
    }

    void SetAll(bool on)
    {
        foreach (var e in _info!.Entries) e.Selected = on;
        _entries.ItemsSource = null;
        _entries.ItemsSource = _info.Entries;
    }

    static Control Header(LinkInfo? i, string heading)
    {
        var thumb = new Thumbs.View(152, 86, 8);
        thumb.Set(i?.Thumbnail ?? "", false, 0);
        var bits = new List<string>();
        if (i == null) bits.Add("These choices are used every time");
        else if (i.IsPlaylist) bits.Add($"Playlist · {i.Entries.Count} videos");
        else if (i.IsDirectFile) { bits.Add("Direct file link"); if (i.DirectSize > 0) bits.Add(Fmt.Bytes(i.DirectSize)); }
        if (i != null && i.Uploader.Length > 0) bits.Add(i.Uploader);
        if (i != null && i.Duration > 0) bits.Add(Fmt.Duration(i.Duration));
        var title = new TextBlock { Text = i?.Title ?? heading, Classes = { "title" }, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis };
        var meta = new TextBlock { Text = string.Join("  ·  ", bits), Classes = { "sub" }, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new DockPanel { Margin = new Thickness(16, 2, 0, 2) };
        DockPanel.SetDock(meta, Dock.Bottom);
        text.Children.AddRange(new Control[] { meta, title });
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Height = 86 };
        Grid.SetColumn(text, 1);
        g.Children.AddRange(new Control[] { thumb, text });
        return g;
    }

    static string EntryText(PlaylistEntry e) => e.Duration > 0 ? $"{e.Title}   ({Fmt.Duration(e.Duration)})" : e.Title;

    void KindChanged()
    {
        bool video = _video.IsChecked == true;
        var list = video ? VideoFormats : AudioFormats;
        _format.ItemsSource = list.Select(f => f.ToUpperInvariant()).ToList();
        int idx = Array.IndexOf(list, Result.Format);
        _format.SelectedIndex = idx >= 0 ? idx : 0;
        _quality.IsVisible = _qualityLabel.IsVisible = video;
        _subs.IsEnabled = video && _info?.IsDirectFile != true;
        SubsChanged();
        FillQuality();
    }

    void SubsChanged()
    {
        bool on = _subs.IsChecked == true && _subs.IsEnabled;
        _embed.IsEnabled = on && Engine.HasFfmpeg;
        _subLangs.IsEnabled = on;
        _subText.IsEnabled = on;
    }

    void FillQuality()
    {
        if (_video.IsChecked != true) return;
        _qualityValues.Clear();
        var items = new List<string>();
        var heights = _info?.Heights.Count > 0
            ? _info.Heights.Where(h => h >= 144).Select(h => h >= 4320 ? 4320 : h >= 2160 ? 2160 : h >= 1440 ? 1440 : h >= 1080 ? 1080 : h >= 720 ? 720 : h >= 480 ? 480 : h >= 360 ? 360 : h >= 240 ? 240 : 144).Distinct().ToList()
            : new List<int> { 4320, 2160, 1440, 1080, 720, 480, 360 };
        items.Add("Best available"); _qualityValues.Add(0);
        foreach (var h in heights)
        {
            string size = "";
            if (_info != null)
            {
                var best = _info.SizeByHeight.Where(kv => kv.Key <= h).OrderByDescending(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault();
                if (best > 0) size = "   ~" + Fmt.Bytes(best + _info.AudioSize);
            }
            items.Add(DownloadOptions.QualityLabel(h) + (h >= 720 && h < 1440 ? " HD" : "") + size);
            _qualityValues.Add(h);
        }
        _quality.ItemsSource = items;
        int want = Result.MaxHeight;
        int sel = _qualityValues.IndexOf(want);
        if (sel < 0 && want > 0) sel = _qualityValues.FindIndex(v => v > 0 && v <= want);
        _quality.SelectedIndex = sel >= 0 ? sel : 0;
    }

    async void Accept()
    {
        var o = Result;
        o.Kind = _audio.IsChecked == true ? MediaKind.Audio : MediaKind.Video;
        o.Format = (_format.SelectedItem?.ToString() ?? "mp4").ToLowerInvariant();
        if (o.Kind == MediaKind.Video && _quality.SelectedIndex >= 0) o.MaxHeight = _qualityValues[_quality.SelectedIndex];
        o.Subtitles = _subs.IsChecked == true && _subs.IsEnabled;
        o.EmbedSubtitles = _embed.IsChecked == true;
        var langs = _useLangList
            ? _langBoxes.Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList()
            : (_subText.Text ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (langs.Count > 0) o.SubtitleLanguages = langs;
        var folder = (_folder.Text ?? "").Trim();
        if (folder.Length == 0) folder = AppState.DefaultFolder();
        try { Directory.CreateDirectory(folder); }
        catch { await Info(this, Title ?? "", "That folder can't be used. Please pick another one."); return; }
        o.Folder = folder;
        o.PlaylistSubfolder = _subfolder.IsChecked == true;

        if (_info?.IsPlaylist == true)
        {
            ChosenEntries = _info.Entries.Where(e => e.Selected).ToList();
            if (ChosenEntries.Count == 0) { await Info(this, Title ?? "", "Pick at least one video from the playlist."); return; }
        }
        Close(true);
    }
}
