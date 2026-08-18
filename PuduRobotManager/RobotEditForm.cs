using System.Net;
using System.Net.Sockets;
using PuduRobotManager.Models;
using PuduRobotManager.Ui;

namespace PuduRobotManager;

public sealed class RobotEditForm : Form
{
    private readonly TextBox _nameBox = new();
    private readonly TextBox _ipBox = new();
    private readonly NumericUpDown _portBox = new();
    private readonly ComboBox _groupBox = new();
    private readonly TextBox _notesBox = new();
    private readonly IReadOnlyList<RobotGroup> _groups;

    public Robot Robot { get; }

    public RobotEditForm(IReadOnlyList<RobotGroup> groups, Robot? robot = null, Guid? defaultGroupId = null)
    {
        _groups = groups;
        var isNew = robot is null;
        Robot = robot is null
            ? new Robot { GroupId = defaultGroupId }
            : new Robot
            {
                Id = robot.Id,
                GroupId = robot.GroupId,
                Name = robot.Name,
                Ip = robot.Ip,
                Port = robot.Port,
                Notes = robot.Notes,
            };

        Text = isNew ? "Add robot" : "Edit robot";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 360);
        Padding = new Padding(16);
        UiTheme.ApplyForm(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        _nameBox.Dock = DockStyle.Fill;
        _ipBox.Dock = DockStyle.Fill;
        _groupBox.Dock = DockStyle.Fill;
        _notesBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(_nameBox);
        UiTheme.StyleTextBox(_ipBox);
        UiTheme.StyleTextBox(_notesBox);
        _groupBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _groupBox.FlatStyle = FlatStyle.Flat;

        _portBox.Dock = DockStyle.Left;
        _portBox.Width = 110;
        _portBox.Minimum = 1;
        _portBox.Maximum = 65535;
        _portBox.Value = Robot.Port is >= 1 and <= 65535 ? Robot.Port : 5555;
        _notesBox.Multiline = true;
        _notesBox.ScrollBars = ScrollBars.Vertical;

        _nameBox.Text = Robot.Name;
        _ipBox.Text = Robot.Ip;
        _notesBox.Text = Robot.Notes;
        FillGroups();

        layout.Controls.Add(FieldLabel("Name"), 0, 0);
        layout.Controls.Add(_nameBox, 1, 0);
        layout.Controls.Add(FieldLabel("IP"), 0, 1);
        layout.Controls.Add(_ipBox, 1, 1);
        layout.Controls.Add(FieldLabel("Port"), 0, 2);
        layout.Controls.Add(_portBox, 1, 2);
        layout.Controls.Add(FieldLabel("Group"), 0, 3);
        layout.Controls.Add(_groupBox, 1, 3);
        layout.Controls.Add(FieldLabel("Notes"), 0, 4);
        layout.Controls.Add(_notesBox, 1, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
        };
        var cancel = UiTheme.CreateButton("Cancel", ButtonKind.Secondary);
        cancel.DialogResult = DialogResult.Cancel;
        var ok = UiTheme.CreateButton("Save", ButtonKind.Primary);
        ok.DialogResult = DialogResult.OK;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);

        ok.Click += (_, _) =>
        {
            if (!ValidateInput())
            {
                DialogResult = DialogResult.None;
                return;
            }

            Robot.Name = _nameBox.Text.Trim();
            Robot.Ip = _ipBox.Text.Trim();
            Robot.Port = (int)_portBox.Value;
            Robot.Notes = _notesBox.Text.Trim();
            Robot.GroupId = _groupBox.SelectedItem is GroupOption option ? option.Id : null;
        };
    }

    private sealed class GroupOption
    {
        public Guid? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public override string ToString() => Name;
    }

    private void FillGroups()
    {
        _groupBox.Items.Clear();
        _groupBox.DisplayMember = nameof(GroupOption.Name);
        _groupBox.Items.Add(new GroupOption { Id = null, Name = "Ungrouped" });
        foreach (var group in _groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            _groupBox.Items.Add(new GroupOption { Id = group.Id, Name = group.Name });
        }

        var selected = _groupBox.Items.Cast<GroupOption>().FirstOrDefault(o => o.Id == Robot.GroupId);
        _groupBox.SelectedItem = selected ?? _groupBox.Items[0];
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Muted,
    };

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "Enter a robot name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _nameBox.Focus();
            return false;
        }

        var ip = _ipBox.Text.Trim();
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            MessageBox.Show(this, "Enter a valid IPv4 address.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _ipBox.Focus();
            return false;
        }

        return true;
    }
}
