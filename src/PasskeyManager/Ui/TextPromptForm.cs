namespace PasskeyManager.Ui;

/// <summary>Small dialog with a multi-line text box.</summary>
public sealed class TextPromptForm : Form
{
    private readonly TextBox _box = new();

    public string Value => _box.Text;

    public TextPromptForm(string title, string prompt, string okText)
    {
        Text = title;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var label = new Label
        {
            Text = prompt,
            Tag = "muted",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 64,
            Padding = new Padding(0, 0, 0, 6),
        };

        _box.Multiline = true;
        _box.AcceptsReturn = true;
        _box.ScrollBars = ScrollBars.Vertical;
        _box.Dock = DockStyle.Fill;
        _box.Font = new Font("Consolas", 10f);

        var inner = new Panel { Dock = DockStyle.Fill, Tag = "surface", Padding = new Padding(12) };
        inner.Controls.Add(_box);
        var card = new Panel { Dock = DockStyle.Fill, Tag = "border", Padding = new Padding(1) };
        card.Controls.Add(inner);

        var ok = new Button { Text = okText, Tag = "primary", DialogResult = DialogResult.OK, Dock = DockStyle.Right };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right };
        var spacer = new Panel { Dock = DockStyle.Right, Width = 8 };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(0, 14, 0, 0) };
        // The control added last is docked first: OK ends up far right, then a gap, then Cancel.
        footer.Controls.Add(cancel);
        footer.Controls.Add(spacer);
        footer.Controls.Add(ok);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 18, 20, 12) };
        body.Controls.Add(card);
        body.Controls.Add(footer);
        body.Controls.Add(label);

        Controls.Add(body);
        CancelButton = cancel;

        Theme.Apply(this);
        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
    }
}
