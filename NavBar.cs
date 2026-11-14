using System.Drawing.Drawing2D;

namespace ClipHarbor;

public class NavItem
{
    public string Key = "", Text = "", Glyph = "";
    public int Count = -1;
    public bool Header;
}

/// <summary>Left sidebar with sections.</summary>
public class NavBar : Control
{
    public readonly List<NavItem> Items = new();
    public string SelectedKey = "all";
    public event Action<string>? Selected;
    public string Footer = "";
    int _hover = -1;
    float S => DeviceDpi / 96f;
    int ItemH => (int)(36 * S);

    public NavBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    Rectangle ItemRect(int i)
    {
        int y = (int)(12 * S);
        for (int k = 0; k < i; k++) y += Items[k].Header ? (int)(34 * S) : ItemH;
        return new Rectangle((int)(10 * S), y, Width - (int)(20 * S), Items[i].Header ? (int)(34 * S) : ItemH - (int)(2 * S));
    }

    int HitIndex(Point p)
    {
        for (int i = 0; i < Items.Count; i++) if (!Items[i].Header && ItemRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitIndex(e.Location);
        if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitIndex(e.Location);
        if (h >= 0) { SelectedKey = Items[h].Key; Invalidate(); Selected?.Invoke(SelectedKey); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; float s = S;
        g.Clear(Theme.Sidebar);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i]; var r = ItemRect(i);
            if (it.Header)
            {
                TextRenderer.DrawText(g, it.Text.ToUpperInvariant(), Theme.Small, new Rectangle(r.X + (int)(10 * s), r.Y + (int)(10 * s), r.Width, r.Height - (int)(10 * s)), Theme.SubText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                continue;
            }
            bool sel = it.Key == SelectedKey;
            if (sel) Theme.FillRound(g, Theme.Dark ? Theme.Hover : Theme.Surface, r, 7 * s);
            else if (i == _hover) Theme.FillRound(g, Theme.Dark ? Color.FromArgb(0x22, 0x25, 0x2B) : Color.FromArgb(0xE2, 0xE5, 0xEB), r, 7 * s);
            if (sel) Theme.FillRound(g, Theme.Accent, new RectangleF(r.X, r.Y + r.Height * 0.25f, 3 * s, r.Height * 0.5f), 1.5f * s);
            var col = sel ? Theme.Accent : Theme.SubText;
            TextRenderer.DrawText(g, it.Glyph, Theme.Icon, new Rectangle(r.X + (int)(10 * s), r.Y, (int)(22 * s), r.Height), col, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, it.Text, sel ? Theme.Bold : Theme.Base, new Rectangle(r.X + (int)(40 * s), r.Y, r.Width - (int)(80 * s), r.Height), sel ? Theme.Text : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (it.Count > 0)
            {
                var c = it.Count.ToString();
                var sz = TextRenderer.MeasureText(c, Theme.Small);
                var pill = new RectangleF(r.Right - sz.Width - 18 * s, r.Y + (r.Height - 18 * s) / 2, sz.Width + 8 * s, 18 * s);
                Theme.FillRound(g, sel ? Theme.Mix(Theme.Accent, Theme.Surface, .82) : (Theme.Dark ? Theme.Border : Theme.Track), pill, 9 * s);
                TextRenderer.DrawText(g, c, Theme.Small, Rectangle.Round(pill), sel ? Theme.Accent : Theme.SubText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
        if (Footer.Length > 0)
        {
            var fr = new Rectangle((int)(20 * s), Height - (int)(64 * s), Width - (int)(36 * s), (int)(52 * s));
            TextRenderer.DrawText(g, Footer, Theme.Small, fr, Theme.SubText, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.WordBreak);
        }
    }
}
