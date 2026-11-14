using System.Collections.Concurrent;
using System.Drawing.Drawing2D;

namespace ClipHarbor;

public static class Thumbs
{
    static readonly ConcurrentDictionary<string, Image?> Cache = new();
    static readonly SemaphoreSlim Gate = new(4);
    public static event Action? Loaded;

    public static Image? Get(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Cache.TryGetValue(url, out var img)) return img;
        Cache[url] = null;
        _ = Task.Run(async () =>
        {
            await Gate.WaitAsync();
            try
            {
                var bytes = await Engine.Http.GetByteArrayAsync(url);
                using var ms = new MemoryStream(bytes);
                using var src = Image.FromStream(ms);
                var bmp = new Bitmap(224, 126);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    // cover-crop to 16:9
                    float ar = (float)src.Width / src.Height, tar = 224f / 126f;
                    RectangleF sr = ar > tar
                        ? new RectangleF((src.Width - src.Height * tar) / 2, 0, src.Height * tar, src.Height)
                        : new RectangleF(0, (src.Height - src.Width / tar) / 2, src.Width, src.Width / tar);
                    g.DrawImage(src, new RectangleF(0, 0, 224, 126), sr, GraphicsUnit.Pixel);
                }
                Cache[url] = bmp;
                Loaded?.Invoke();
            }
            catch { }
            finally { Gate.Release(); }
        });
        return null;
    }

    public static void DrawPlaceholder(Graphics g, RectangleF r, bool audio, float s)
    {
        using var br = new LinearGradientBrush(r, Theme.Mix(Theme.Accent, Theme.Surface, Theme.Dark ? .55 : .35), Theme.Mix(Theme.Accent, Color.FromArgb(0x8A, 0x4F, 0xE0), .6), 35f);
        using var p = Theme.Round(r, 6 * s);
        g.FillPath(br, p);
        var glyph = audio ? "" : "";
        using var f = new Font("Segoe MDL2 Assets", 16f);
        TextRenderer.DrawText(g, glyph, f, Rectangle.Round(r), Color.FromArgb(230, 255, 255, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

public enum RowAction { None, Primary, Folder, Remove }

/// <summary>Owner-drawn, scrollable list of downloads.</summary>
public class DownloadList : Control
{
    public List<DownloadItem> Source = new();
    public Func<DownloadItem, bool> Filter = _ => true;
    public string Search = "";
    public readonly HashSet<string> SelectedIds = new();
    public event Action<DownloadItem, RowAction>? ActionClicked;
    public event Action<DownloadItem>? Opened;
    public event Action<Point>? MenuRequested;
    public event Action? SelectionChanged;
    public string EmptyTitle = "Nothing here yet";
    public string EmptyHint = "";

    readonly VScrollBar _scroll = new();
    readonly ToolTip _tip = new();
    static readonly Font EmptyIcon = new("Segoe MDL2 Assets", 30f);
    static readonly Font StatusIcon = new("Segoe MDL2 Assets", 8.5f);
    List<DownloadItem> _view = new();
    int _hoverRow = -1; RowAction _hoverAction;
    int _anchor = -1;

    float S => DeviceDpi / 96f;
    int RowH => (int)(78 * S);

    public DownloadList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        _scroll.Dock = DockStyle.Right;
        _scroll.Visible = false;
        _scroll.Scroll += (_, _) => Invalidate();
        _scroll.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scroll);
        TabStop = true;
        Thumbs.Loaded += () => { if (IsHandleCreated) BeginInvoke(Invalidate); };
    }

    public List<DownloadItem> View => _view;
    public IEnumerable<DownloadItem> Selected => _view.Where(i => SelectedIds.Contains(i.Id));

    public void Rebuild()
    {
        var q = Search.Trim();
        _view = Source.Where(Filter)
            .Where(i => q.Length == 0 || i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || i.Url.Contains(q, StringComparison.OrdinalIgnoreCase) || i.Uploader.Contains(q, StringComparison.OrdinalIgnoreCase) || i.Playlist.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Added).ToList();
        SelectedIds.RemoveWhere(id => !_view.Any(v => v.Id == id));
        UpdateScroll();
        Invalidate();
    }

    void UpdateScroll()
    {
        int content = _view.Count * RowH + (int)(16 * S);
        if (content > Height)
        {
            _scroll.Visible = true;
            _scroll.Maximum = content;
            _scroll.LargeChange = Math.Max(1, Height);
            _scroll.SmallChange = RowH / 2;
            if (_scroll.Value > content - Height) _scroll.Value = Math.Max(0, content - Height);
        }
        else { _scroll.Value = 0; _scroll.Visible = false; }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScroll(); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_scroll.Visible) return;
        int v = _scroll.Value - e.Delta * RowH / 120;
        _scroll.Value = Math.Clamp(v, 0, Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1));
    }

    int Top0 => (int)(8 * S) - (_scroll.Visible ? _scroll.Value : 0);
    int ContentW => Width - (_scroll.Visible ? _scroll.Width : 0);

    Rectangle RowRect(int i) => new((int)(12 * S), Top0 + i * RowH, ContentW - (int)(24 * S), RowH - (int)(6 * S));

    Rectangle ActionRect(Rectangle row, int slot)
    {
        int b = (int)(32 * S);
        int x = row.Right - (int)(10 * S) - (3 - slot) * (b + (int)(4 * S));
        return new Rectangle(x, row.Y + (row.Height - b) / 2, b, b);
    }

    (int row, RowAction act) Hit(Point p)
    {
        if (p.X >= ContentW) return (-1, RowAction.None);
        int i = (p.Y - Top0) / RowH;
        if (p.Y < Top0 || i < 0 || i >= _view.Count) return (-1, RowAction.None);
        var r = RowRect(i);
        if (!r.Contains(p)) return (-1, RowAction.None);
        if (ActionRect(r, 0).Contains(p)) return (i, RowAction.Primary);
        if (ActionRect(r, 1).Contains(p)) return (i, RowAction.Folder);
        if (ActionRect(r, 2).Contains(p)) return (i, RowAction.Remove);
        return (i, RowAction.None);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var (r, a) = Hit(e.Location);
        if (r != _hoverRow || a != _hoverAction)
        {
            if (a != _hoverAction || r != _hoverRow) _tip.SetToolTip(this, r >= 0 && a != RowAction.None ? Tooltip(_view[r], a) : "");
            _hoverRow = r; _hoverAction = a; Invalidate();
        }
        Cursor = a != RowAction.None ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverRow = -1; _hoverAction = RowAction.None; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var (r, a) = Hit(e.Location);
        if (r < 0) { if (e.Button == MouseButtons.Left) { SelectedIds.Clear(); SelectionChanged?.Invoke(); Invalidate(); } return; }
        var it = _view[r];
        if (e.Button == MouseButtons.Left && a != RowAction.None) { ActionClicked?.Invoke(it, a); return; }
        if (e.Button == MouseButtons.Right)
        {
            if (!SelectedIds.Contains(it.Id)) { SelectedIds.Clear(); SelectedIds.Add(it.Id); _anchor = r; }
        }
        else if ((ModifierKeys & Keys.Shift) != 0 && _anchor >= 0)
        {
            SelectedIds.Clear();
            for (int k = Math.Min(_anchor, r); k <= Math.Max(_anchor, r); k++) SelectedIds.Add(_view[k].Id);
        }
        else if ((ModifierKeys & Keys.Control) != 0)
        {
            if (!SelectedIds.Remove(it.Id)) SelectedIds.Add(it.Id);
            _anchor = r;
        }
        else { SelectedIds.Clear(); SelectedIds.Add(it.Id); _anchor = r; }
        SelectionChanged?.Invoke();
        Invalidate();
        if (e.Button == MouseButtons.Right) MenuRequested?.Invoke(e.Location);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        var (r, a) = Hit(e.Location);
        if (r >= 0 && a == RowAction.None) Opened?.Invoke(_view[r]);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A) { foreach (var v in _view) SelectedIds.Add(v.Id); SelectionChanged?.Invoke(); Invalidate(); e.Handled = true; }
        if ((e.KeyCode is Keys.Up or Keys.Down) && _view.Count > 0)
        {
            int cur = _anchor < 0 ? 0 : Math.Clamp(_anchor + (e.KeyCode == Keys.Down ? 1 : -1), 0, _view.Count - 1);
            SelectedIds.Clear(); SelectedIds.Add(_view[cur].Id); _anchor = cur;
            var rr = RowRect(cur);
            if (_scroll.Visible)
            {
                if (rr.Top < 0) _scroll.Value = Math.Max(0, _scroll.Value + rr.Top - (int)(8 * S));
                else if (rr.Bottom > Height) _scroll.Value = Math.Min(_scroll.Maximum - _scroll.LargeChange + 1, _scroll.Value + rr.Bottom - Height + (int)(8 * S));
            }
            SelectionChanged?.Invoke(); Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float s = S;

        if (_view.Count == 0) { PaintEmpty(g, s); return; }

        int first = Math.Max(0, (-Top0) / RowH);
        for (int i = first; i < _view.Count; i++)
        {
            var r = RowRect(i);
            if (r.Top > Height) break;
            PaintRow(g, _view[i], r, i, s);
        }
    }

    void PaintEmpty(Graphics g, float s)
    {
        int cx = ContentW / 2, cy = Height / 2;
        var circle = new RectangleF(cx - 48 * s, cy - 110 * s, 96 * s, 96 * s);
        using (var br = new SolidBrush(Theme.Mix(Theme.Accent, Theme.Back, .86)))
            g.FillEllipse(br, circle);
        TextRenderer.DrawText(g, "", EmptyIcon, Rectangle.Round(circle), Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var t = new Rectangle(0, (int)(cy + 2 * s), ContentW, (int)(32 * s));
        TextRenderer.DrawText(g, EmptyTitle, Theme.Title, t, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var h = new Rectangle((int)(40 * s), (int)(cy + 36 * s), ContentW - (int)(80 * s), (int)(60 * s));
        TextRenderer.DrawText(g, EmptyHint, Theme.Base, h, Theme.SubText, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
    }

    void PaintRow(Graphics g, DownloadItem it, Rectangle r, int i, float s)
    {
        bool sel = SelectedIds.Contains(it.Id), hover = i == _hoverRow;
        Theme.FillRound(g, sel ? Theme.Selected : hover ? Theme.Hover : Theme.Surface, r, 8 * s);
        Theme.DrawRound(g, sel ? Theme.Mix(Theme.Accent, Theme.Surface, .5) : Theme.Border, r, 8 * s);

        // thumbnail
        var th = new RectangleF(r.X + 10 * s, r.Y + 8 * s, (r.Height - 16 * s) * 16f / 9f, r.Height - 16 * s);
        var img = Thumbs.Get(it.ThumbnailUrl);
        if (img != null)
        {
            using var clip = Theme.Round(th, 6 * s);
            var st = g.Save(); g.SetClip(clip); g.DrawImage(img, th); g.Restore(st);
        }
        else Thumbs.DrawPlaceholder(g, th, it.Options.Kind == MediaKind.Audio, s);
        if (it.DurationSeconds > 0)
        {
            var d = Fmt.Duration(it.DurationSeconds);
            var sz = TextRenderer.MeasureText(d, Theme.Small);
            var dr = new RectangleF(th.Right - sz.Width - 6 * s, th.Bottom - sz.Height - 4 * s, sz.Width + 2 * s, sz.Height);
            Theme.FillRound(g, Color.FromArgb(190, 0, 0, 0), dr, 3 * s);
            TextRenderer.DrawText(g, d, Theme.Small, Rectangle.Round(dr), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        int x = (int)(th.Right + 14 * s);
        int right = ActionRect(r, 0).Left - (int)(12 * s);
        int w = Math.Max(40, right - x);

        TextRenderer.DrawText(g, it.Title.Length > 0 ? it.Title : it.Url, Theme.Bold, new Rectangle(x, r.Y + (int)(9 * s), w, (int)(20 * s)), Theme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left);

        var bits = new List<string> { it.Direct ? "File" : it.Options.Describe() };
        if (it.TotalBytes > 0) bits.Add(Fmt.Bytes(it.TotalBytes));
        if (it.Options.Subtitles && it.Options.Kind == MediaKind.Video) bits.Add("Subtitles");
        if (it.Uploader.Length > 0) bits.Add(it.Uploader);
        if (it.Playlist.Length > 0) bits.Add("▸ " + it.Playlist);
        TextRenderer.DrawText(g, string.Join("  ·  ", bits), Theme.Small, new Rectangle(x, r.Y + (int)(30 * s), w, (int)(18 * s)), Theme.SubText,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // status line
        int sy = r.Y + (int)(51 * s);
        string status; Color sc = Theme.SubText;
        switch (it.Status)
        {
            case ItemStatus.Downloading:
                var bar = new RectangleF(x, sy + 6 * s, Math.Min(w * 0.55f, 360 * s), 5 * s);
                Theme.FillRound(g, Theme.Track, bar, 2.5f * s);
                if (it.Phase == "processing")
                {
                    Theme.FillRound(g, Theme.Good, bar, 2.5f * s);
                    status = "Finishing up…";
                }
                else
                {
                    if (it.Progress > 0) Theme.FillRound(g, Theme.Accent, new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, bar.Width * (float)it.Progress), bar.Height), 2.5f * s);
                    var parts = new List<string> { it.Progress > 0 ? $"{it.Progress * 100:0}%" : "Starting…" };
                    if (it.Phase == "audio") parts.Add("audio");
                    if (it.Speed > 0) parts.Add(Fmt.Bytes(it.Speed) + "/s");
                    if (it.Eta > 0) parts.Add(Fmt.Duration(it.Eta) + " left");
                    status = string.Join("  ·  ", parts);
                }
                TextRenderer.DrawText(g, status, Theme.Small, new Rectangle((int)(bar.Right + 10 * s), sy, Math.Max(10, right - (int)(bar.Right + 10 * s)), (int)(18 * s)), Theme.Text,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                break;
            default:
                (status, sc) = it.Status switch
                {
                    ItemStatus.Queued => ("Waiting in line", Theme.SubText),
                    ItemStatus.Paused => ("Paused" + (it.Progress > 0 ? $" at {it.Progress * 100:0}%" : ""), Theme.Warn),
                    ItemStatus.Done => ("Downloaded  ·  " + it.Added.ToString("MMM d, HH:mm"), Theme.Good),
                    ItemStatus.Canceled => ("Stopped", Theme.SubText),
                    _ => ("Failed: " + it.Message, Theme.Bad),
                };
                var glyph = it.Status switch { ItemStatus.Done => "", ItemStatus.Failed => "", ItemStatus.Paused => "", ItemStatus.Queued => "", _ => "" };
                TextRenderer.DrawText(g, glyph, StatusIcon, new Rectangle(x, sy, (int)(16 * s), (int)(18 * s)), sc, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, status, Theme.Small, new Rectangle(x + (int)(18 * s), sy, w - (int)(18 * s), (int)(18 * s)), sc,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                break;
        }

        // actions
        string primary = it.Status switch
        {
            ItemStatus.Downloading or ItemStatus.Queued => "",
            ItemStatus.Done => "",
            ItemStatus.Paused => "",
            _ => "",
        };
        string[] glyphs = { primary, "", "" };
        for (int k = 0; k < 3; k++)
        {
            var ar = ActionRect(r, k);
            bool hv = hover && (int)_hoverAction == k + 1;
            if (hv) Theme.FillRound(g, Theme.Dark ? Theme.Border : Theme.Track, ar, 6 * s);
            var col = k == 0 && it.Status == ItemStatus.Done ? Theme.Accent : (hv ? Theme.Text : Theme.SubText);
            if (k == 1 && it.Status != ItemStatus.Done && string.IsNullOrEmpty(it.Options.Folder)) col = Theme.Border;
            TextRenderer.DrawText(g, glyphs[k], Theme.Icon, ar, col, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public static string Tooltip(DownloadItem it, RowAction a) => a switch
    {
        RowAction.Primary => it.Status switch
        {
            ItemStatus.Downloading or ItemStatus.Queued => "Pause",
            ItemStatus.Done => "Play",
            ItemStatus.Paused => "Resume",
            _ => "Try again",
        },
        RowAction.Folder => "Show in folder",
        RowAction.Remove => "Remove from list",
        _ => ""
    };
}
