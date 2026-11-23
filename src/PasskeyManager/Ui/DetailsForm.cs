using System.Text;
using PasskeyManager.Core;

namespace PasskeyManager.Ui;

/// <summary>Shows everything known about one passkey.</summary>
public sealed class DetailsForm : Form
{
    private readonly PasskeyEntry _e;
    private readonly int _sameSite;

    private readonly DataGridView _overview = new();
    private readonly TextBox _winBox = new();
    private readonly TextBox _filesBox = new();
    private readonly Panel _pOverview = new() { Dock = DockStyle.Fill };
    private readonly Panel _pWin = new() { Dock = DockStyle.Fill };
    private readonly Panel _pFiles = new() { Dock = DockStyle.Fill };
    private readonly Label _toast = new() { Tag = "muted", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

    private Button _tabOverview = null!, _tabWin = null!, _tabFiles = null!;
    private int _tab;

    public DetailsForm(PasskeyEntry entry, int samePasskeysAtSite)
    {
        _e = entry;
        _sameSite = samePasskeysAtSite;

        Text = "Passkey details";
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 700);
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        BuildUi();
        FillOverview();
        SelectTab(0);

        Theme.Apply(this);
        ColorOverviewFields();
        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Shown += async (_, _) =>
        {
            _overview.ClearSelection();
            await LoadExtrasAsync();
        };
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(24, 16, 24, 6) };
        var title = new Label
        {
            Text = _e.SiteLabel,
            Font = new Font("Segoe UI Semibold", 17f),
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        var sub = new Label
        {
            Text = "User ID: " + _e.UserIdDisplay,
            Tag = "muted",
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        header.Controls.Add(sub);
        header.Controls.Add(title);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(24, 6, 24, 6),
            WrapContents = false,
        };
        _tabOverview = NavButton("Overview", 0);
        _tabWin = NavButton("Windows details", 1);
        _tabFiles = NavButton("Related files", 2);
        nav.Controls.AddRange(new Control[] { _tabOverview, _tabWin, _tabFiles });

        _overview.Dock = DockStyle.Fill;
        _overview.ReadOnly = true;
        _overview.AllowUserToAddRows = false;
        _overview.AllowUserToDeleteRows = false;
        _overview.AllowUserToResizeRows = false;
        _overview.RowHeadersVisible = false;
        _overview.ColumnHeadersVisible = false;
        _overview.AutoGenerateColumns = false;
        _overview.MultiSelect = false;
        _overview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _overview.RowTemplate.Height = 34;
        _overview.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "field", Width = 250, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Resizable = DataGridViewTriState.False,
        });
        _overview.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "value", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _overview.Columns[1].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _overview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _overview.CellDoubleClick += (_, ev) =>
        {
            if (ev.RowIndex < 0) return;
            var v = Convert.ToString(_overview.Rows[ev.RowIndex].Cells[1].Value) ?? "";
            CopyText(v, "Value copied");
        };
        Theme.EnableDoubleBuffer(_overview);
        _pOverview.Controls.Add(Card(_overview, surfacePadding: false));

        ConfigureMono(_winBox);
        ConfigureMono(_filesBox);
        _pWin.Controls.Add(Card(_winBox, surfacePadding: true));
        _pFiles.Controls.Add(Card(_filesBox, surfacePadding: true));

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 4, 24, 0) };
        content.Controls.Add(_pOverview);
        content.Controls.Add(_pWin);
        content.Controls.Add(_pFiles);

        var copy = new Button { Text = "Copy this tab", Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 8, 0) };
        copy.Click += (_, _) => CopyCurrentTab();
        var close = new Button { Text = "Close", Tag = "primary", Anchor = AnchorStyles.Right, Margin = new Padding(0), DialogResult = DialogResult.OK };
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(24, 8, 24, 12),
            ColumnCount = 3,
            RowCount = 1,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        footer.Controls.Add(_toast, 0, 0);
        footer.Controls.Add(copy, 1, 0);
        footer.Controls.Add(close, 2, 0);

        // The control added last is docked first, so the Fill panel goes in first.
        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(nav);
        Controls.Add(header);
        CancelButton = close;
        AcceptButton = close;
    }

    private Button NavButton(string text, int index)
    {
        var b = new Button { Text = text, Tag = "ghost", Margin = new Padding(0, 0, 6, 0) };
        b.Click += (_, _) => SelectTab(index);
        return b;
    }

    private static Panel Card(Control inner, bool surfacePadding)
    {
        inner.Dock = DockStyle.Fill;
        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            Tag = "surface",
            Padding = surfacePadding ? new Padding(14) : new Padding(0),
        };
        surface.Controls.Add(inner);
        var border = new Panel { Dock = DockStyle.Fill, Tag = "border", Padding = new Padding(1) };
        border.Controls.Add(surface);
        return border;
    }

    private static void ConfigureMono(TextBox box)
    {
        box.Multiline = true;
        box.ReadOnly = true;
        box.WordWrap = false;
        box.ScrollBars = ScrollBars.Both;
        box.Font = new Font("Consolas", 9.5f);
    }

    private void SelectTab(int index)
    {
        _tab = index;
        _pOverview.Visible = index == 0;
        _pWin.Visible = index == 1;
        _pFiles.Visible = index == 2;
        _tabOverview.Tag = index == 0 ? "primary" : "ghost";
        _tabWin.Tag = index == 1 ? "primary" : "ghost";
        _tabFiles.Tag = index == 2 ? "primary" : "ghost";
        Theme.StyleButton(_tabOverview);
        Theme.StyleButton(_tabWin);
        Theme.StyleButton(_tabFiles);
        _toast.Text = index switch
        {
            0 => "Double-click a row to copy its value.",
            1 => "Raw output from Windows (certutil), shown exactly as returned.",
            _ => "Best-effort search of Windows Hello's protected folder.",
        };
    }

    private void FillOverview()
    {
        string Opt(string s) => s.Length > 0 ? s : "(not available)";

        var rows = new (string Field, string Value)[]
        {
            ("Site", _e.Site ?? "Not identified yet. Use \"Add sites…\" or \"Import bookmarks\" in the main window."),
            ("Site hash (SHA-256 of site name)", Opt(_e.RpIdHash)),
            ("User ID (as text)", _e.UserIdDisplay),
            ("User ID (hex)", Opt(_e.UserIdHex)),
            ("User ID (Base64URL, as sites see it)", Opt(_e.UserIdBase64Url)),
            ("User ID length", _e.UserIdByteLength > 0 ? $"{_e.UserIdByteLength} bytes" : "(unknown)"),
            ("Passkeys for this site on this PC", _sameSite.ToString()),
            ("First seen by this app", FirstSeenText()),
            ("Created / last used", "Not provided by Windows for these keys. See the other tabs for anything Windows does report."),
            ("Display name (e.g. \"Admin\")", "Not part of the key name. Windows shows it from protected credential data this tool can't read."),
            ("Extra key-name parts", _e.NameTail.Length > 0 ? _e.NameTail + "  (meaning not documented)" : "(none)"),
            ("Key store", "Windows Hello / NGC (certutil -csp NGC)"),
            ("Full key name", _e.KeyName),
        };
        foreach (var (f, v) in rows) _overview.Rows.Add(f, v);
    }

    private string FirstSeenText()
    {
        if (_e.FirstSeen is not { } d) return "—";
        return _e.FirstSeenIsBaseline
            ? $"It already existed when this app first scanned on {d:yyyy-MM-dd HH:mm} (real creation date unknown)."
            : $"{d:yyyy-MM-dd HH:mm}  (the first time this app saw it, so it was created on or shortly before this)";
    }

    private void ColorOverviewFields()
    {
        foreach (DataGridViewRow r in _overview.Rows)
        {
            r.Cells[0].Style.ForeColor = Theme.P.Muted;
            r.Cells[0].Style.Font = new Font("Segoe UI Semibold", 9.5f);
        }
    }

    private async Task LoadExtrasAsync()
    {
        _winBox.Text = "Running certutil…";
        _filesBox.Text = "Searching the Windows Hello folder…";

        var winTask = PasskeyStore.GetDetailsAsync(_e);
        var filesTask = Task.Run(() => NgcFiles.Find(_e));

        try
        {
            var text = await winTask;
            if (!IsDisposed) _winBox.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
        catch (Exception ex)
        {
            if (!IsDisposed) _winBox.Text = "Could not run certutil: " + ex.Message;
        }

        try
        {
            var (hits, note) = await filesTask;
            if (!IsDisposed) _filesBox.Text = FormatFiles(hits, note);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) _filesBox.Text = "Search failed: " + ex.Message;
        }
    }

    private static string FormatFiles(IReadOnlyList<FileHit> hits, string note)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Folder searched:");
        sb.AppendLine("  " + NgcFiles.Root);
        sb.AppendLine();
        if (note.Length > 0) { sb.AppendLine(note); sb.AppendLine(); }

        if (hits.Count == 0)
        {
            sb.AppendLine("No files or folders were found whose names contain this key's user ID or name suffix.");
            sb.AppendLine();
            sb.AppendLine("That is normal: Windows may store the key under a different, unrelated name, and the folder");
            sb.AppendLine("is protected. File times, even when found, are not guaranteed to be the passkey's creation time.");
            return sb.ToString();
        }

        sb.AppendLine($"Found {hits.Count} item(s):");
        foreach (var h in hits)
        {
            sb.AppendLine();
            sb.AppendLine($"{(h.IsDirectory ? "[folder]" : "[file]  ")} {h.Path}");
            sb.AppendLine($"    matched on : {h.MatchedOn}");
            sb.AppendLine($"    created    : {h.Created:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"    modified   : {h.Modified:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"    accessed   : {h.Accessed:yyyy-MM-dd HH:mm:ss}");
            if (!h.IsDirectory) sb.AppendLine($"    size       : {h.Size:N0} bytes");
        }
        sb.AppendLine();
        sb.AppendLine("These are file-system times, not guaranteed to equal the passkey's creation or last-use time.");
        return sb.ToString();
    }

    private void CopyCurrentTab()
    {
        string text = _tab switch
        {
            0 => string.Join(Environment.NewLine, _overview.Rows.Cast<DataGridViewRow>()
                    .Select(r => $"{r.Cells[0].Value}: {r.Cells[1].Value}")),
            1 => _winBox.Text,
            _ => _filesBox.Text,
        };
        CopyText(text, "Copied to clipboard");
    }

    private void CopyText(string text, string message)
    {
        if (text.Length == 0) return;
        try
        {
            Clipboard.SetText(text);
            _toast.Text = message;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _toast.Text = "Clipboard is busy, try again.";
        }
    }
}
