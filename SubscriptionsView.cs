namespace ClipHarbor;

/// <summary>Follow channels or playlists; new videos download automatically.</summary>
public class SubscriptionsView : Panel
{
    readonly AppState _state;
    readonly MainForm _main;
    readonly Panel _head = new() { Dock = DockStyle.Top, Height = 92 };
    readonly ListView _lv = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None, OwnerDraw = true, MultiSelect = true };
    readonly FlatButton _add = new() { Text = "Add subscription", Glyph = "", Primary = true, Size = new Size(170, 34) };
    readonly FlatButton _check = new() { Text = "Check now", Glyph = "", Size = new Size(124, 34) };
    readonly FlatButton _edit = new() { Text = "Choices…", Size = new Size(100, 34) };
    readonly FlatButton _remove = new() { Text = "Remove", Glyph = "", Size = new Size(110, 34) };
    readonly Label _title = new() { Text = "Subscriptions", AutoSize = true, Font = Theme.Title };
    readonly Label _hint = new() { Text = "Follow a channel or playlist and new videos are downloaded for you automatically.", AutoSize = true, Tag = "sub" };
    bool _checking;

    public SubscriptionsView(AppState state, MainForm main)
    {
        _state = state; _main = main;
        _lv.Columns.Add("Name", 280);
        _lv.Columns.Add("Saved as", 130);
        _lv.Columns.Add("Last checked", 150);
        _lv.Columns.Add("Status", 300);
        _lv.DrawColumnHeader += (_, e) =>
        {
            using var b = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header!.Text, Theme.Bold, Rectangle.Inflate(e.Bounds, -6, 0), Theme.SubText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            using var p = new Pen(Theme.Border);
            e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        };
        _lv.DrawItem += (_, e) => { };
        _lv.DrawSubItem += (_, e) =>
        {
            var sel = e.Item!.Selected;
            using var b = new SolidBrush(sel ? Theme.Selected : Theme.Surface);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.ColumnIndex == 0 ? Theme.Bold : Theme.Base, Rectangle.Inflate(e.Bounds, -6, 0),
                e.ColumnIndex == 0 ? Theme.Text : Theme.SubText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        _lv.SmallImageList = new ImageList { ImageSize = new Size(1, 34) };
        _lv.DoubleClick += (_, _) => EditChoices();

        _head.Controls.AddRange(new Control[] { _title, _hint, _add, _check, _edit, _remove });
        _head.Resize += (_, _) => LayoutHead();
        _add.Click += async (_, _) => await AddSubscription();
        _check.Click += async (_, _) => await CheckAll(manual: true);
        _edit.Click += (_, _) => EditChoices();
        _remove.Click += (_, _) => RemoveSelected();
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 20) };
        pad.Controls.Add(_lv);
        Controls.Add(pad);
        Controls.Add(_head);
    }

    void LayoutHead()
    {
        float s = DeviceDpi / 96f;
        _title.Location = new Point((int)(20 * s), (int)(14 * s));
        _hint.Location = new Point((int)(20 * s), (int)(42 * s));
        int x = _head.Width - (int)(20 * s);
        foreach (var b in new[] { _remove, _edit, _check, _add })
        {
            b.Width = b.Measure().Width; x -= b.Width;
            b.Location = new Point(x, (int)(16 * s));
            x -= (int)(8 * s);
        }
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Back;
        _head.BackColor = Theme.Back;
        _lv.BackColor = Theme.Surface; _lv.ForeColor = Theme.Text;
        _title.ForeColor = Theme.Text; _hint.ForeColor = Theme.SubText;
        Invalidate(true);
    }

    public void Rebuild()
    {
        _lv.BeginUpdate();
        _lv.Items.Clear();
        foreach (var s in _state.Subscriptions)
        {
            var li = new ListViewItem(s.Name.Length > 0 ? s.Name : s.Url) { Tag = s };
            li.SubItems.Add(s.Options.Describe());
            li.SubItems.Add(s.LastChecked == default ? "Never" : s.LastChecked.ToString("MMM d, HH:mm"));
            li.SubItems.Add(s.LastResult);
            _lv.Items.Add(li);
        }
        if (_state.Subscriptions.Count == 0)
            _lv.Items.Add(new ListViewItem(new[] { "No subscriptions yet", "", "", "Press “Add subscription” and paste a channel or playlist link" }));
        _lv.EndUpdate();
        _check.Enabled = !_checking && _state.Subscriptions.Count > 0;
    }

    IEnumerable<Subscription> Selected => _lv.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<Subscription>();

    async Task AddSubscription()
    {
        if (!Engine.HasEngine)
        {
            MessageBox.Show(FindForm(), "Subscriptions need the download engine. Open Settings and press Install engine first.", "Subscriptions");
            return;
        }
        string clip = "";
        try { if (Clipboard.ContainsText()) clip = Clipboard.GetText().Trim(); } catch { }
        using var p = new PromptDialog("Add subscription", "Link to a channel or playlist:", Engine.IsUrl(clip) ? clip : "");
        if (p.ShowDialog(FindForm()) != DialogResult.OK) return;
        var url = p.Value.Trim();
        if (!Engine.IsUrl(url)) return;
        _add.Enabled = false; _add.Text = "Looking…";
        LinkInfo info;
        try { info = await Engine.AnalyzeAsync(url, CancellationToken.None, 50); }
        catch (Exception ex) { MessageBox.Show(FindForm(), "Couldn't read that link: " + ex.Message, "Subscriptions"); return; }
        finally { _add.Enabled = true; _add.Text = "Add subscription"; }
        if (!info.IsPlaylist)
        {
            MessageBox.Show(FindForm(), "That link is a single video. Use the link to the channel's videos page or a playlist.", "Subscriptions");
            return;
        }
        using var d = new OptionsDialog(null, _state.Settings.LastOptions, "Subscribe to " + (info.Title.Length > 0 ? info.Title : "channel"));
        if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
        var sub = new Subscription { Url = url, Name = info.Title.Length > 0 ? info.Title : url, Options = d.Result.Clone(), LastChecked = DateTime.Now };
        foreach (var e in info.Entries) sub.Seen.Add(e.Id);
        sub.LastResult = $"Following · {info.Entries.Count} existing videos skipped";
        _state.Subscriptions.Add(sub);
        int recent = Math.Min(5, info.Entries.Count);
        if (recent > 0 && MessageBox.Show(FindForm(), $"Also download the newest {recent} videos now?", "Subscriptions", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            _main.Queue(info, sub.Options, info.Entries.Take(recent).ToList(), sub.Name);
        _main.Changed();
        Rebuild();
    }

    void EditChoices()
    {
        var s = Selected.FirstOrDefault();
        if (s == null) return;
        using var d = new OptionsDialog(null, s.Options, "Choices for " + s.Name);
        if (d.ShowDialog(FindForm()) == DialogResult.OK) { s.Options = d.Result.Clone(); _main.Changed(); Rebuild(); }
    }

    void RemoveSelected()
    {
        var sel = Selected.ToList();
        if (sel.Count == 0) return;
        foreach (var s in sel) _state.Subscriptions.Remove(s);
        _main.Changed(); Rebuild();
    }

    public async Task AutoCheck()
    {
        var hours = _state.Settings.SubscriptionCheckHours;
        if (hours <= 0 || !Engine.HasEngine) return;
        if (_state.Subscriptions.Any(s => s.Enabled && (DateTime.Now - s.LastChecked).TotalHours >= hours))
            await CheckAll(manual: false);
    }

    async Task CheckAll(bool manual)
    {
        if (_checking || !Engine.HasEngine) return;
        _checking = true; _check.Enabled = false; _check.Text = "Checking…";
        int total = 0;
        try
        {
            foreach (var s in _state.Subscriptions.ToList())
            {
                if (!s.Enabled) continue;
                if (!manual && (DateTime.Now - s.LastChecked).TotalHours < _state.Settings.SubscriptionCheckHours) continue;
                s.LastResult = "Checking…"; Rebuild();
                try
                {
                    var info = await Engine.AnalyzeAsync(s.Url, CancellationToken.None, 30);
                    var fresh = info.Entries.Where(e => !s.Seen.Contains(e.Id)).ToList();
                    foreach (var e in fresh) s.Seen.Add(e.Id);
                    if (fresh.Count > 0) _main.Queue(info, s.Options, fresh, s.Name);
                    total += fresh.Count;
                    s.LastResult = fresh.Count > 0 ? $"{fresh.Count} new video{(fresh.Count == 1 ? "" : "s")} added" : "Up to date";
                }
                catch (Exception ex) { s.LastResult = "Problem: " + ex.Message; }
                s.LastChecked = DateTime.Now;
                Rebuild();
            }
        }
        finally
        {
            _checking = false; _check.Text = "Check now";
            Rebuild(); _main.Changed();
        }
    }
}

public class PromptDialog : Form
{
    readonly TextBox _box = new();
    public string Value => _box.Text;

    public PromptDialog(string title, string label, string value)
    {
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = title; Font = Theme.Base;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(20, 18) });
        _box.Location = new Point(20, 44); _box.Width = 520; _box.Text = value;
        Controls.Add(_box);
        var ok = new FlatButton { Text = "OK", Primary = true, Size = new Size(100, 34), Location = new Point(440, 86) };
        var cancel = new FlatButton { Text = "Cancel", Size = new Size(100, 34), Location = new Point(330, 86) };
        ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.AddRange(new Control[] { ok, cancel });
        ClientSize = new Size(560, 136);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); } else if (e.KeyCode == Keys.Escape) Close(); };
        Theme.Style(this);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.TitleBar(this); }
}
