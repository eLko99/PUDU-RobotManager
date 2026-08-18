using PuduRobotManager.Ui;

namespace PuduRobotManager;

public sealed class GroupEditForm : Form
{
    private readonly TextBox _nameBox = new();

    public string GroupName => _nameBox.Text.Trim();

    public GroupEditForm(string? currentName = null)
    {
        Text = string.IsNullOrWhiteSpace(currentName) ? "New group" : "Rename group";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 150);
        Padding = new Padding(16);
        UiTheme.ApplyForm(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "Group name",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = UiTheme.Muted,
        };
        _nameBox.Dock = DockStyle.Fill;
        _nameBox.Text = currentName ?? string.Empty;
        UiTheme.StyleTextBox(_nameBox);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
        };
        var cancel = UiTheme.CreateButton("Cancel", ButtonKind.Secondary);
        cancel.DialogResult = DialogResult.Cancel;
        var ok = UiTheme.CreateButton("Save", ButtonKind.Primary);
        ok.DialogResult = DialogResult.OK;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_nameBox, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show(this, "Enter a group name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                _nameBox.Focus();
            }
        };
    }
}
