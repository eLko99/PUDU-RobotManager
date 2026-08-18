using PuduRobotManager.Models;
using PuduRobotManager.Ui;

namespace PuduRobotManager;

public sealed class SettingsForm : Form
{
    private readonly TextBox _adbPathBox = new();
    private readonly TextBox _remotePathBox = new();

    public string AdbPath => _adbPathBox.Text.Trim();
    public string RemoteDesktopExePath => _remotePathBox.Text.Trim();

    public SettingsForm(AppConfig config)
    {
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 250);
        Padding = new Padding(16);
        UiTheme.ApplyForm(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        _adbPathBox.Dock = DockStyle.Fill;
        _remotePathBox.Dock = DockStyle.Fill;
        _adbPathBox.Margin = new Padding(0, 8, 0, 8);
        _remotePathBox.Margin = new Padding(0, 8, 0, 8);
        UiTheme.StyleTextBox(_adbPathBox);
        UiTheme.StyleTextBox(_remotePathBox);
        _adbPathBox.Text = config.AdbPath;
        _remotePathBox.Text = config.RemoteDesktopExePath;

        var browseAdb = UiTheme.CreateButton("Browse…", ButtonKind.Secondary);
        UiTheme.FitInCell(browseAdb);
        browseAdb.Margin = new Padding(8, 6, 0, 6);
        var browseRemote = UiTheme.CreateButton("Browse…", ButtonKind.Secondary);
        UiTheme.FitInCell(browseRemote);
        browseRemote.Margin = new Padding(8, 6, 0, 6);
        browseAdb.Click += (_, _) => BrowseForExe(_adbPathBox, "ADB executable", "adb.exe");
        browseRemote.Click += (_, _) => BrowseForExe(_remotePathBox, "Remote desktop executable", "scrcpy.exe");

        layout.Controls.Add(FieldLabel("ADB path"), 0, 0);
        layout.Controls.Add(_adbPathBox, 1, 0);
        layout.Controls.Add(browseAdb, 2, 0);
        layout.Controls.Add(FieldLabel("Remote desktop"), 0, 1);
        layout.Controls.Add(_remotePathBox, 1, 1);
        layout.Controls.Add(browseRemote, 2, 1);

        var hint = new Label
        {
            Text = "Leave ADB blank to use adb from PATH. Remote desktop looks for a scrcpy folder next to this application.",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            Padding = new Padding(0, 8, 0, 0),
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        var cancel = UiTheme.CreateButton("Cancel", ButtonKind.Secondary);
        cancel.DialogResult = DialogResult.Cancel;
        var ok = UiTheme.CreateButton("Save", ButtonKind.Primary);
        ok.DialogResult = DialogResult.OK;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 3);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);

        ok.Click += (_, _) =>
        {
            if (!ValidatePath(_adbPathBox.Text, "ADB path")
                || !ValidatePath(_remotePathBox.Text, "Remote desktop path"))
            {
                DialogResult = DialogResult.None;
            }
        };
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Muted,
    };

    private void BrowseForExe(TextBox target, string title, string fileName)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = fileName,
            CheckFileExists = true,
        };

        var current = target.Text.Trim();
        if (File.Exists(current))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(current);
            dialog.FileName = Path.GetFileName(current);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private bool ValidatePath(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
        {
            return true;
        }

        MessageBox.Show(this, $"{label} does not exist:\n{path}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
}
