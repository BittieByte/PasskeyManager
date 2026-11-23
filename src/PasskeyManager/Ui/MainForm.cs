using System.Text;
using PasskeyManager.Core;

namespace PasskeyManager.Ui;

public sealed class MainForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _filter = new();
    private readonly Label _subtitle = new() { Tag = "muted", AutoSize = true, Dock = DockStyle.Top };
    private readonly Label _footerInfo = new() { Tag = "muted", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ContextMenuStrip _menu = new();
    private readonly SiteResolver _sites = new();
    private readonly Font _boldFont;

    private Button _refresh = null!, _addSites = null!, _import = null!, _details = null!,
        _export = null!, _delete = null!, _themeBtn = null!, _checkAll = null!, _clear = null!;

    private List<PasskeyEntry> _entries = new();
    private bool _busy;
    private bool _populating;

    public MainForm()
    {
        Theme.DetectSystem();

        Text = "Passkey Manager";
        Font = new Font("Segoe UI", 9.5f);
        _boldFont = new Font(Font, FontStyle.Bold);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1180, 700);
        MinimumSize = new Size(900, 480);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        BuildMenu();
        ApplyTheme();

        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(24, 14, 24, 6) };
        var title = new Label
        {
            Text = "Passkey Manager",
            Font = new Font("Segoe UI Semibold", 19f),
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        _subtitle.Text = "Reading passkeys…";
        _themeBtn = MakeButton("Dark mode", "ghost", (_, _) => ToggleTheme());
        _themeBtn.Dock = DockStyle.Right;
        header.Controls.Add(_subtitle);
        header.Controls.Add(title);
        header.Controls.Add(_themeBtn);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(24, 10, 24, 10) };
        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };

        _filter.PlaceholderText = "Search sites or IDs…";
        _filter.Dock = DockStyle.Fill;
        _filter.TextChanged += (_, _) => Populate();
        var searchInner = new Panel { Dock = DockStyle.Fill, Tag = "surface", Padding = new Padding(10, 9, 10, 0) };
        searchInner.Controls.Add(_filter);
        var search = new Panel { Width = 270, Height = 38, Tag = "border", Padding = new Padding(1), Margin = new Padding(0, 0, 14, 0) };
        search.Controls.Add(searchInner);

        _refresh = MakeButton("Refresh", "secondary", async (_, _) => await LoadAsync());
        _addSites = MakeButton("Add sites…", "secondary", OnAddSites);
        _import = MakeButton("Import bookmarks", "secondary", OnImportBookmarks);
        _details = MakeButton("Details", "secondary", OnDetails);
        _export = MakeButton("Export list…", "secondary", OnExport);
        foreach (var b in new[] { _refresh, _addSites, _import, _details, _export }) b.Margin = new Padding(0, 0, 8, 0);
        left.Controls.Add(search);
        left.Controls.AddRange(new Control[] { _refresh, _addSites, _import, _details, _export });

        _delete = MakeButton("Delete checked…", "danger", OnDelete);
        _delete.Dock = DockStyle.Right;
        toolbar.Controls.Add(left);
        toolbar.Controls.Add(_delete);

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
        _checkAll = MakeButton("Check all shown", "secondary", (_, _) => SetAllChecked(true));
        _clear = MakeButton("Clear checks", "secondary", (_, _) => SetAllChecked(false));
        _checkAll.Anchor = AnchorStyles.Right;
        _clear.Anchor = AnchorStyles.Right;
        _checkAll.Margin = new Padding(0, 0, 8, 0);
        _clear.Margin = new Padding(0);
        footer.Controls.Add(_footerInfo, 0, 0);
        footer.Controls.Add(_checkAll, 1, 0);
        footer.Controls.Add(_clear, 2, 0);

        ConfigureGrid();
        var card = new Panel { Dock = DockStyle.Fill, Tag = "border", Padding = new Padding(1) };
        card.Controls.Add(_grid);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 4, 24, 0) };
        body.Controls.Add(card);

        // The control added last is docked first, so the Fill panel goes in first.
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(toolbar);
        Controls.Add(header);
    }

    private static Button MakeButton(string text, string kind, EventHandler onClick)
    {
        var b = new Button { Text = text, Tag = kind };
        b.Click += onClick;
        return b;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.AutoGenerateColumns = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 38;
        Theme.EnableDoubleBuffer(_grid);

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "chk", HeaderText = "", Width = 52,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Resizable = DataGridViewTriState.False,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "site", HeaderText = "Site", ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 32, MinimumWidth = 150,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "count", HeaderText = "At site", ReadOnly = true, Width = 80,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "user", HeaderText = "User ID", ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 38, MinimumWidth = 150,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "seen", HeaderText = "First seen by this app", ReadOnly = true, Width = 190,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "hash", HeaderText = "Site hash", ReadOnly = true, Width = 140,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        foreach (DataGridViewColumn c in _grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
        _grid.Columns["hash"]!.DefaultCellStyle.Font = new Font("Consolas", 9f);

        // Commit checkbox clicks immediately so the counts update.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (!_populating && e.ColumnIndex == 0) UpdateStatus();
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex != 0) OpenDetails(RowEntry(e.RowIndex));
        };
        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[1];
        };
        _grid.ContextMenuStrip = _menu;
    }

    private void BuildMenu()
    {
        _menu.Items.Add("Details…", null, (_, _) => OpenDetails(CurrentEntry()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Check all for this site", null, (_, _) => SetSiteChecked(true));
        _menu.Items.Add("Uncheck all for this site", null, (_, _) => SetSiteChecked(false));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Copy site hash", null, (_, _) => CopyOf(CurrentEntry()?.RpIdHash));
        _menu.Items.Add("Copy user ID (hex)", null, (_, _) => CopyOf(CurrentEntry()?.UserIdHex));
        _menu.Items.Add("Copy full key name", null, (_, _) => CopyOf(CurrentEntry()?.KeyName));
    }

    private static void CopyOf(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }

    private void ApplyTheme()
    {
        Theme.Apply(this);
        Theme.StyleMenu(_menu);
        Theme.ApplyTitleBar(this);
        _themeBtn.Text = Theme.IsDark ? "Light mode" : "Dark mode";
    }

    private void ToggleTheme()
    {
        Theme.Toggle();
        ApplyTheme();
        Populate();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true, "Reading passkeys from Windows…");
        try
        {
            var found = await PasskeyStore.ListAsync();
            _entries = found.ToList();
            _sites.Resolve(_entries);
            SeenStore.Apply(_entries);
            Populate();
        }
        catch (Exception ex)
        {
            _entries = new List<PasskeyEntry>();
            Populate();
            MessageBox.Show(this, "Could not read passkeys:\n\n" + ex.Message, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void Populate()
    {
        var needle = _filter.Text.Trim();
        var keep = CheckedEntries().Select(e => e.KeyName).ToHashSet(StringComparer.Ordinal);
        var p = Theme.P;

        var counts = _entries.GroupBy(e => e.RpIdHash).ToDictionary(g => g.Key, g => g.Count());
        var visible = _entries
            .Where(e => needle.Length == 0
                || e.SiteLabel.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.UserIdDisplay.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.UserIdHex.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.RpIdHash.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Site == null)
            .ThenBy(e => e.SiteLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.RpIdHash, StringComparer.Ordinal)
            .ThenBy(e => e.FirstSeen)
            .ToList();

        _populating = true;
        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            var band = 0;
            string? last = null;
            foreach (var e in visible)
            {
                if (last != null && e.RpIdHash != last) band++;
                last = e.RpIdHash;

                var shortHash = e.RpIdHash.Length >= 12 ? e.RpIdHash[..12] + "…" : e.RpIdHash;
                var idx = _grid.Rows.Add(keep.Contains(e.KeyName), e.SiteLabel, counts[e.RpIdHash].ToString(),
                    e.UserIdDisplay, e.FirstSeenLabel, shortHash);

                var row = _grid.Rows[idx];
                row.Tag = e;
                row.DefaultCellStyle.BackColor = band % 2 == 0 ? p.Surface : p.Band;
                if (e.Site != null) row.Cells[1].Style.Font = _boldFont;
                else row.Cells[1].Style.ForeColor = p.Muted;
                row.Cells[4].Style.ForeColor = p.Muted;
                row.Cells[5].Style.ForeColor = p.Muted;
                row.Cells[1].ToolTipText = $"Site hash: {e.RpIdHash}";
                row.Cells[3].ToolTipText = $"User ID (hex): {e.UserIdHex}";
            }
            _grid.ClearSelection();
        }
        finally
        {
            _grid.ResumeLayout();
            _populating = false;
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var sites = _entries.Select(e => e.RpIdHash).Where(h => h.Length > 0).Distinct().Count();
        var named = _entries.Where(e => e.Site != null).Select(e => e.RpIdHash).Distinct().Count();
        _subtitle.Text = _entries.Count == 0
            ? "No passkeys found on this PC."
            : $"{_entries.Count} passkeys  •  {sites} sites  •  {named} identified";

        var n = CheckedEntries().Count;
        _footerInfo.Text = n == 0
            ? "Tick the boxes of passkeys to remove. Double-click a row for full details."
            : $"{n} checked for deletion";
    }

    private void SetBusy(bool busy, string? message)
    {
        _busy = busy;
        UseWaitCursor = busy;
        foreach (var b in new[] { _refresh, _addSites, _import, _details, _export, _delete, _checkAll, _clear })
            b.Enabled = !busy;
        if (message != null) _subtitle.Text = message; else UpdateStatus();
    }

    private List<PasskeyEntry> CheckedEntries() =>
        _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Cells[0].Value is bool b && b)
            .Select(r => (PasskeyEntry)r.Tag!)
            .ToList();

    private PasskeyEntry? RowEntry(int rowIndex) =>
        rowIndex >= 0 && rowIndex < _grid.Rows.Count ? _grid.Rows[rowIndex].Tag as PasskeyEntry : null;

    private PasskeyEntry? CurrentEntry() => _grid.CurrentRow?.Tag as PasskeyEntry;

    private void SetAllChecked(bool value)
    {
        _populating = true;
        foreach (DataGridViewRow r in _grid.Rows) r.Cells[0].Value = value;
        _populating = false;
        UpdateStatus();
    }

    private void SetSiteChecked(bool value)
    {
        var cur = CurrentEntry();
        if (cur == null) return;
        _populating = true;
        foreach (DataGridViewRow r in _grid.Rows)
            if (r.Tag is PasskeyEntry e && e.RpIdHash == cur.RpIdHash) r.Cells[0].Value = value;
        _populating = false;
        UpdateStatus();
    }

    private void OnDetails(object? sender, EventArgs e) => OpenDetails(CurrentEntry());

    private void OpenDetails(PasskeyEntry? entry)
    {
        if (entry == null)
        {
            MessageBox.Show(this, "Select a passkey row first.", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var same = _entries.Count(x => x.RpIdHash == entry.RpIdHash);
        using var f = new DetailsForm(entry, same);
        f.ShowDialog(this);
    }

    private void OnAddSites(object? sender, EventArgs e)
    {
        using var dlg = new TextPromptForm(
            "Add sites",
            "Type or paste the websites you use passkeys on, one per line (full URLs are fine).\nExample: github.com",
            "Add");
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Value)) return;

        var before = _entries.Count(x => x.Site != null);
        var added = _sites.AddFromText(dlg.Value);
        _sites.Resolve(_entries);
        Populate();

        var matched = _entries.Count(x => x.Site != null) - before;
        MessageBox.Show(this,
            $"{added} site(s) added. {matched} more passkey(s) are now matched to a site name." +
            (matched == 0 ? "\n\nNo new matches. The name has to be exactly what the passkey was created for; try the domain from the address bar, with and without \"www.\"." : ""),
            "Add sites", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void OnImportBookmarks(object? sender, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true, "Scanning browser bookmarks…");
        try
        {
            var scan = await Task.Run(BrowserBookmarks.Collect);
            var before = _entries.Count(x => x.Site != null);
            var added = _sites.AddHosts(scan.Hosts);
            _sites.Resolve(_entries);
            Populate();
            var matched = _entries.Count(x => x.Site != null) - before;

            MessageBox.Show(this,
                scan.FilesScanned == 0
                    ? "No bookmark files were found for Brave, Chrome or Edge under this Windows account."
                    : $"Read {scan.FilesScanned} bookmark file(s) from Brave / Chrome / Edge and added {added} site(s).\n\n{matched} more passkey(s) are now matched to a site name.",
                "Import bookmarks", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        if (_entries.Count == 0) return;
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"passkeys-{DateTime.Now:yyyyMMdd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            BackupStore.WriteCsv(dlg.FileName, _entries);
            MessageBox.Show(this,
                "List exported.\n\nThis is a list for your records. The passkeys' private keys cannot be exported from Windows.",
                "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        if (_busy) return;
        var selected = CheckedEntries();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Check the passkeys you want to remove first.", "Nothing checked",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Permanently delete {selected.Count} passkey(s) from this PC?");
        sb.AppendLine();
        foreach (var s in selected.Take(8)) sb.AppendLine($"  •  {s.SiteLabel}  —  {s.UserIdDisplay}");
        if (selected.Count > 8) sb.AppendLine($"  …and {selected.Count - 8} more");
        sb.AppendLine();
        sb.AppendLine("This cannot be undone. The website keeps its own record, so afterwards sign in another way " +
                      "(for example with a password) and register a fresh passkey if you still want one.");

        if (MessageBox.Show(this, sb.ToString(), "Delete passkeys", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        SetBusy(true, "Deleting…");
        try
        {
            string record;
            try
            {
                record = BackupStore.WriteDeletionRecord(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not write the deletion record, so nothing was deleted.\n\n" +
                    "Records are saved in a \"backups\" folder next to this program:\n" + AppPaths.BackupDirPath +
                    "\n\nMake sure that folder is writable (or move the program to a folder you can write to).\n\n" + ex.Message,
                    "Aborted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var failures = new List<string>();
            var done = 0;
            foreach (var s in selected)
            {
                _subtitle.Text = $"Deleting {++done} of {selected.Count}…";
                var (ok, msg) = await PasskeyStore.DeleteAsync(s);
                if (!ok) failures.Add($"{s.SiteLabel} — {s.UserIdDisplay}\n    {msg}");
            }

            var result = $"Deleted {selected.Count - failures.Count} of {selected.Count} passkey(s).\n\nRecord saved to:\n{record}";
            if (failures.Count > 0)
                result += "\n\nFailed:\n" + string.Join("\n", failures) + "\n\n(Deleting needs administrator rights.)";
            MessageBox.Show(this, result, "Done", MessageBoxButtons.OK,
                failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, null);
        }
        await LoadAsync();
    }
}
