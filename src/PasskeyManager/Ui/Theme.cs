using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PasskeyManager.Ui;

public sealed record Palette(
    Color Back, Color Surface, Color Surface2, Color Text, Color Muted,
    Color Border, Color Accent, Color AccentText, Color Danger, Color Band, Color Selection);

/// <summary>Light and dark palettes and helpers that restyle controls into a flat look.</summary>
public static class Theme
{
    private static Color C(string hex) => ColorTranslator.FromHtml(hex);

    public static readonly Palette Light = new(
        Back: C("#F3F4F6"), Surface: C("#FFFFFF"), Surface2: C("#F9FAFB"),
        Text: C("#111827"), Muted: C("#6B7280"), Border: C("#E5E7EB"),
        Accent: C("#2563EB"), AccentText: Color.White, Danger: C("#DC2626"),
        Band: C("#F6F8FB"), Selection: C("#DBEAFE"));

    public static readonly Palette Dark = new(
        Back: C("#0F1115"), Surface: C("#181B22"), Surface2: C("#1F232C"),
        Text: C("#E5E7EB"), Muted: C("#9CA3AF"), Border: C("#2A2F3A"),
        Accent: C("#3B82F6"), AccentText: Color.White, Danger: C("#EF4444"),
        Band: C("#1C2029"), Selection: C("#243B63"));

    public static bool IsDark { get; private set; }
    public static Palette P => IsDark ? Dark : Light;

    /// <summary>Starts in whatever mode Windows apps are set to use.</summary>
    public static void DetectSystem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            IsDark = key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception) { IsDark = false; }
    }

    public static void Toggle() => IsDark = !IsDark;

    public static void Apply(Control root)
    {
        var p = P;
        switch (root)
        {
            case Form f:
                f.BackColor = p.Back;
                f.ForeColor = p.Text;
                break;
            case DataGridView g:
                StyleGrid(g);
                return;
            case Button b:
                StyleButton(b);
                return;
            case TextBox t:
                t.BackColor = p.Surface;
                t.ForeColor = p.Text;
                t.BorderStyle = BorderStyle.None;
                break;
            case Label l:
                l.BackColor = Color.Transparent;
                l.ForeColor = (l.Tag as string) switch
                {
                    "muted" => p.Muted,
                    "accent" => p.Accent,
                    _ => p.Text,
                };
                break;
            case Panel pn:
                pn.BackColor = (pn.Tag as string) switch
                {
                    "surface" => p.Surface,
                    "border" => p.Border,
                    _ => p.Back,
                };
                break;
        }
        foreach (Control c in root.Controls) Apply(c);
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    /// <summary>Button.Tag chooses the look: "primary", "danger", "ghost", otherwise a bordered button.</summary>
    public static void StyleButton(Button b)
    {
        var p = P;
        var kind = b.Tag as string ?? "secondary";

        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.Cursor = Cursors.Hand;
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.Padding = new Padding(12, 6, 12, 6);
        b.FlatAppearance.BorderSize = 1;

        Color back, fore, border;
        switch (kind)
        {
            case "primary": back = p.Accent; fore = p.AccentText; border = p.Accent; break;
            case "danger": back = p.Danger; fore = Color.White; border = p.Danger; break;
            case "ghost": back = p.Back; fore = p.Text; border = p.Back; break;
            default: back = p.Surface; fore = p.Text; border = p.Border; break;
        }

        var tint = IsDark ? Color.White : Color.Black;
        b.BackColor = back;
        b.ForeColor = fore;
        b.FlatAppearance.BorderColor = border;
        b.FlatAppearance.MouseOverBackColor = Mix(back, tint, 0.10);
        b.FlatAppearance.MouseDownBackColor = Mix(back, tint, 0.18);
    }

    /// <summary>Flat, borderless table styling.</summary>
    public static void StyleGrid(DataGridView g)
    {
        var p = P;
        g.BorderStyle = BorderStyle.None;
        g.BackgroundColor = p.Surface;
        g.GridColor = p.Border;
        g.EnableHeadersVisualStyles = false;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        var head = g.ColumnHeadersDefaultCellStyle;
        head.BackColor = p.Surface2;
        head.ForeColor = p.Muted;
        head.SelectionBackColor = p.Surface2;
        head.SelectionForeColor = p.Muted;
        head.Padding = new Padding(8, 0, 8, 0);
        head.Font = new Font("Segoe UI Semibold", 9f);

        var cell = g.DefaultCellStyle;
        cell.BackColor = p.Surface;
        cell.ForeColor = p.Text;
        cell.SelectionBackColor = p.Selection;
        cell.SelectionForeColor = p.Text;
        cell.Padding = new Padding(8, 0, 8, 0);
    }

    public static void EnableDoubleBuffer(Control c) =>
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(c, true);

    public static void StyleMenu(ContextMenuStrip menu)
    {
        var p = P;
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(p)) { RoundedEdges = false };
        menu.ShowImageMargin = false;
        menu.BackColor = p.Surface;
        menu.ForeColor = p.Text;
        foreach (ToolStripItem item in menu.Items) item.ForeColor = p.Text;
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        private readonly Palette _p;
        public MenuColors(Palette p) { _p = p; UseSystemColors = false; }
        public override Color ToolStripDropDownBackground => _p.Surface;
        public override Color ImageMarginGradientBegin => _p.Surface;
        public override Color ImageMarginGradientMiddle => _p.Surface;
        public override Color ImageMarginGradientEnd => _p.Surface;
        public override Color MenuBorder => _p.Border;
        public override Color MenuItemBorder => _p.Selection;
        public override Color MenuItemSelected => _p.Selection;
        public override Color MenuItemSelectedGradientBegin => _p.Selection;
        public override Color MenuItemSelectedGradientEnd => _p.Selection;
        public override Color SeparatorDark => _p.Border;
        public override Color SeparatorLight => _p.Border;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Makes the title bar dark or light (Windows 10 2004+ and Windows 11).</summary>
    public static void ApplyTitleBar(Form f)
    {
        if (!f.IsHandleCreated) return;
        try
        {
            var v = IsDark ? 1 : 0;
            DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int));
            DwmSetWindowAttribute(f.Handle, 19, ref v, sizeof(int));
        }
        catch (Exception) { }
    }
}
