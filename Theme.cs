using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClipHarbor;

public static class Theme
{
    public static bool Dark;
    public static Color Back, Surface, Sidebar, Border, Text, SubText, Accent, AccentHover, AccentText, Hover, Selected, Good, Bad, Warn, Track, InputBack;

    public static readonly Font Base = new("Segoe UI", 9.75f);
    public static readonly Font Bold = new("Segoe UI Semibold", 9.75f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Title = new("Segoe UI Semibold", 13f);
    public static readonly Font Big = new("Segoe UI Semibold", 16f);
    public static readonly Font Icon = new("Segoe MDL2 Assets", 11f);
    public static readonly Font IconBig = new("Segoe MDL2 Assets", 40f);

    public static void Apply(string name)
    {
        Dark = name == "Dark" || (name == "System" && SystemPrefersDark());
        Accent = Color.FromArgb(0x2F, 0x6F, 0xEB);
        AccentHover = Color.FromArgb(0x24, 0x5C, 0xCF);
        AccentText = Color.White;
        Good = Color.FromArgb(0x1E, 0xA4, 0x6A);
        Bad = Color.FromArgb(0xDC, 0x3B, 0x3B);
        Warn = Color.FromArgb(0xD9, 0x8B, 0x0B);
        if (Dark)
        {
            Back = Color.FromArgb(0x1C, 0x1E, 0x23);
            Surface = Color.FromArgb(0x25, 0x28, 0x2E);
            Sidebar = Color.FromArgb(0x17, 0x19, 0x1D);
            Border = Color.FromArgb(0x36, 0x3A, 0x42);
            Text = Color.FromArgb(0xEC, 0xEE, 0xF2);
            SubText = Color.FromArgb(0x9A, 0xA1, 0xAE);
            Hover = Color.FromArgb(0x2D, 0x31, 0x38);
            Selected = Color.FromArgb(0x26, 0x36, 0x55);
            Track = Color.FromArgb(0x3A, 0x3F, 0x48);
            InputBack = Color.FromArgb(0x2A, 0x2D, 0x34);
        }
        else
        {
            Back = Color.FromArgb(0xF5, 0xF6, 0xF8);
            Surface = Color.White;
            Sidebar = Color.FromArgb(0xEC, 0xEE, 0xF2);
            Border = Color.FromArgb(0xDD, 0xE0, 0xE6);
            Text = Color.FromArgb(0x1D, 0x21, 0x29);
            SubText = Color.FromArgb(0x6A, 0x72, 0x80);
            Hover = Color.FromArgb(0xF0, 0xF3, 0xF8);
            Selected = Color.FromArgb(0xE3, 0xEC, 0xFD);
            Track = Color.FromArgb(0xE3, 0xE6, 0xEB);
            InputBack = Color.White;
        }
    }

    static bool SystemPrefersDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void TitleBar(Form f)
    {
        try { int v = Dark ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
    }

    /// <summary>Colours a form and its standard controls.</summary>
    public static void Style(Control root)
    {
        foreach (Control c in root.Controls) Style(c);
        switch (root)
        {
            case Form f: f.BackColor = Surface; f.ForeColor = Text; f.Font = Base; break;
            case FlatButton: break;
            case TextBox t: t.BackColor = InputBack; t.ForeColor = Text; t.BorderStyle = BorderStyle.FixedSingle; break;
            case NumericUpDown n: n.BackColor = InputBack; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox cb: cb.BackColor = InputBack; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Flat; break;
            case CheckedListBox cl: cl.BackColor = InputBack; cl.ForeColor = Text; cl.BorderStyle = BorderStyle.FixedSingle; break;
            case ListBox lb: lb.BackColor = InputBack; lb.ForeColor = Text; lb.BorderStyle = BorderStyle.FixedSingle; break;
            case CheckBox ch: ch.ForeColor = Text; ch.BackColor = Color.Transparent; break;
            case RadioButton rb: rb.ForeColor = Text; rb.BackColor = Color.Transparent; break;
            case Label l when l.Tag as string == "sub": l.ForeColor = SubText; l.BackColor = Color.Transparent; break;
            case Label l: l.ForeColor = Text; l.BackColor = Color.Transparent; break;
            case TabPage tp: tp.BackColor = Surface; tp.ForeColor = Text; break;
            case Panel p when p.Tag as string == "card": p.BackColor = Back; break;
            case Panel or TableLayoutPanel or FlowLayoutPanel: root.BackColor = Color.Transparent; root.ForeColor = Text; break;
        }
    }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
    {
        using var b = new SolidBrush(c);
        using var p = Round(r, rad);
        g.FillPath(b, p);
    }

    public static void DrawRound(Graphics g, Color c, RectangleF r, float rad, float w = 1)
    {
        using var pen = new Pen(c, w);
        using var p = Round(r, rad);
        g.DrawPath(pen, p);
    }

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}

/// <summary>Flat rounded button with optional icon glyph (Segoe MDL2 Assets).</summary>
public class FlatButton : Control
{
    public bool Primary { get; set; }
    public bool Toggle { get; set; }
    public bool Checked { get; set; }
    public string Glyph { get; set; } = "";
    bool _hover, _down;

    public FlatButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        Font = Theme.Bold;
        Height = 34;
        BackColor = Color.Transparent;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnClick(EventArgs e) { if (Toggle) Checked = !Checked; Invalidate(); base.OnClick(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty); base.OnKeyDown(e); }

    public Size Measure()
    {
        using var g = CreateGraphics();
        float s = DeviceDpi / 96f;
        var w = TextRenderer.MeasureText(g, Text, Font).Width;
        if (Glyph.Length > 0) w += (int)(26 * s);
        return new Size(w + (int)(28 * s), Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color bg, fg, border = Color.Empty;
        bool on = Toggle && Checked;
        if (!Enabled) { bg = Theme.Track; fg = Theme.SubText; }
        else if (Primary || on) { bg = _down ? Theme.Mix(Theme.AccentHover, Color.Black, .1) : _hover ? Theme.AccentHover : Theme.Accent; fg = Theme.AccentText; }
        else { bg = _down ? Theme.Track : _hover ? Theme.Hover : Theme.Surface; fg = Theme.Text; border = Theme.Border; }
        Theme.FillRound(g, bg, r, 6 * s);
        if (border != Color.Empty) Theme.DrawRound(g, border, r, 6 * s);
        if (Focused && ShowFocusCues) Theme.DrawRound(g, Theme.Accent, RectangleF.Inflate(r, -2, -2), 5 * s);

        var textSize = TextRenderer.MeasureText(g, Text, Font);
        int total = textSize.Width + (Glyph.Length > 0 ? (int)((Text.Length > 0 ? 24 : 16) * s) : 0);
        int x = (Width - total) / 2;
        if (Glyph.Length > 0)
        {
            TextRenderer.DrawText(g, Glyph, Theme.Icon, new Rectangle(x, 0, (int)(18 * s), Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            x += (int)(24 * s);
        }
        if (Text.Length > 0)
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textSize.Width + 4, Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
