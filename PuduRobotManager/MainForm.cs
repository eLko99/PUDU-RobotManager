using PuduRobotManager.Models;
using PuduRobotManager.Services;
using PuduRobotManager.Ui;

namespace PuduRobotManager;

public sealed class MainForm : Form
{
    private readonly ConfigStore _store = new();
    private readonly AdbService _adb;
    private readonly RemoteDesktopLauncher _remoteDesktop;
    private readonly ListBox _groupList = new();
    private readonly DataGridView _grid = new();
    private readonly Label _listTitle = new();
    private readonly Label _emptyLabel = new();
    private readonly Label _connectionLabel = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _adbStatusLabel = new();
    private readonly ToolStripStatusLabel _lastResultLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly Button _renameGroupButton;
    private readonly Button _deleteGroupButton;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Button _connectButton;
    private readonly Button _disconnectButton;
    private readonly Button _openRemoteButton;
    private readonly Button _connectAndOpenButton;

    private AppConfig _config;
    private IReadOnlyList<AdbDevice> _devices = [];
    private bool _busy;
    private bool _refreshing;
    private Guid? _selectedGroupId;
    private bool _showUngroupedOnly;
    private bool _splitterReady;

    public MainForm()
    {
        _config = _store.Load();
        _adb = new AdbService(() => _config.AdbPath);
        _remoteDesktop = new RemoteDesktopLauncher(() => _config.RemoteDesktopExePath);

        _renameGroupButton = UiTheme.CreateButton("Rename", ButtonKind.Ghost);
        _deleteGroupButton = UiTheme.CreateButton("Delete", ButtonKind.Ghost);
        _editButton = UiTheme.CreateButton("Edit", ButtonKind.Secondary);
        _deleteButton = UiTheme.CreateButton("Delete", ButtonKind.Secondary);
        _connectButton = UiTheme.CreateButton("Connect", ButtonKind.Secondary);
        _disconnectButton = UiTheme.CreateButton("Disconnect", ButtonKind.Danger);
        _openRemoteButton = UiTheme.CreateButton("Remote desktop", ButtonKind.Secondary);
        _connectAndOpenButton = UiTheme.CreateButton("Connect and Open", ButtonKind.Primary);

        var tips = new ToolTip();
        tips.SetToolTip(_disconnectButton, "Disconnect all ADB sessions");
        tips.SetToolTip(_connectAndOpenButton, "Connect via ADB and open remote desktop");

        Text = "PUDU Robot Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 600);
        Size = new Size(1140, 680);
        UiTheme.ApplyForm(this);

        var header = BuildHeader();
        var toolbar = BuildToolbar();
        var body = BuildBody();
        ConfigureStatus();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(body, 0, 2);
        root.Controls.Add(_status, 0, 3);
        Controls.Add(root);

        _refreshTimer.Interval = 3000;
        _refreshTimer.Tick += async (_, _) => await RefreshDevicesAsync(showErrors: false);
        Load += async (_, _) =>
        {
            BindGroups();
            BindRobots();
            UpdateButtons();
            await RefreshDevicesAsync(showErrors: false);
            _refreshTimer.Start();
        };
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer.Stop();
        UseWaitCursor = true;
        try
        {
            _adb.DisconnectAllAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Closing should not be blocked if ADB is unavailable.
        }
        finally
        {
            UseWaitCursor = false;
        }

        base.OnFormClosing(e);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Header,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(16, 6, 12, 6),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        var title = new Label
        {
            Text = "PUDU Robot Manager",
            Font = UiTheme.TitleFont,
            ForeColor = Color.White,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0),
        };
        var subtitle = new Label
        {
            Text = "Remote ADB connections",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UiTheme.HeaderMuted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0, 2, 0, 0),
        };
        titles.Controls.Add(title, 0, 0);
        titles.Controls.Add(subtitle, 0, 1);

        _connectionLabel.AutoSize = true;
        _connectionLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _connectionLabel.ForeColor = UiTheme.HeaderMuted;
        _connectionLabel.TextAlign = ContentAlignment.MiddleRight;
        _connectionLabel.Anchor = AnchorStyles.None;
        _connectionLabel.Margin = new Padding(12, 0, 8, 0);
        UpdateConnectionLabel();

        var settings = UiTheme.CreateButton("Settings", ButtonKind.Header);
        settings.Margin = new Padding(0);
        settings.Anchor = AnchorStyles.None;
        settings.Click += (_, _) => OpenSettings();

        header.Controls.Add(titles, 0, 0);
        header.Controls.Add(_connectionLabel, 1, 0);
        header.Controls.Add(settings, 2, 0);
        return header;
    }

    private Control BuildToolbar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            Padding = new Padding(16, 10, 16, 10),
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoSize = true,
        };

        var addButton = UiTheme.CreateButton("Add robot", ButtonKind.Secondary);
        addButton.Click += (_, _) => AddRobot();
        _editButton.Click += (_, _) => EditRobot();
        _deleteButton.Click += (_, _) => DeleteRobot();
        _connectButton.Click += async (_, _) => await RunBusyAsync(() => ConnectAsync(openRemote: false));
        _disconnectButton.Click += async (_, _) => await RunBusyAsync(DisconnectAllAsync);
        _openRemoteButton.Click += (_, _) => OpenRemoteDesktop();
        _connectAndOpenButton.Click += async (_, _) => await RunBusyAsync(() => ConnectAsync(openRemote: true));

        var spacer = new Label { Width = 12, AutoSize = false, Margin = new Padding(0) };

        layout.Controls.Add(addButton);
        layout.Controls.Add(_editButton);
        layout.Controls.Add(_deleteButton);
        layout.Controls.Add(spacer);
        layout.Controls.Add(_connectButton);
        layout.Controls.Add(_disconnectButton);
        layout.Controls.Add(_openRemoteButton);
        layout.Controls.Add(_connectAndOpenButton);
        panel.Controls.Add(layout);

        var border = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
        panel.Controls.Add(border);
        return panel;
    }

    private Control BuildBody()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = UiTheme.Border,
        };
        split.Panel1.BackColor = UiTheme.Sidebar;
        split.Panel2.BackColor = UiTheme.Background;
        split.Panel1.Padding = new Padding(12);
        split.Panel2.Padding = new Padding(12, 12, 12, 8);

        split.Panel1.Controls.Add(BuildGroupPanel());
        split.Panel2.Controls.Add(BuildRobotPanel());

        Shown += (_, _) => LayoutSplitter(split);
        split.SizeChanged += (_, _) =>
        {
            if (split.IsHandleCreated && split.Width > 0)
            {
                LayoutSplitter(split);
            }
        };
        return split;
    }

    private void LayoutSplitter(SplitContainer split)
    {
        if (split.Width < 100)
        {
            return;
        }

        var panel1Min = Math.Min(200, Math.Max(50, split.Width / 4));
        var panel2Min = Math.Min(360, Math.Max(80, split.Width / 2));
        if (panel1Min + panel2Min + split.SplitterWidth >= split.Width)
        {
            return;
        }

        split.Panel1MinSize = panel1Min;
        split.Panel2MinSize = panel2Min;
        if (_splitterReady)
        {
            return;
        }

        var max = split.Width - split.Panel2MinSize - split.SplitterWidth;
        split.SplitterDistance = Math.Clamp(250, split.Panel1MinSize, max);
        _splitterReady = true;
    }

    private Control BuildGroupPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));

        var heading = new Label
        {
            Text = "GROUPS",
            Font = UiTheme.SectionFont,
            ForeColor = UiTheme.Muted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _groupList.Dock = DockStyle.Fill;
        _groupList.BorderStyle = BorderStyle.None;
        _groupList.IntegralHeight = false;
        _groupList.DrawMode = DrawMode.OwnerDrawFixed;
        _groupList.ItemHeight = 36;
        _groupList.BackColor = UiTheme.Sidebar;
        _groupList.DrawItem += GroupList_DrawItem;
        _groupList.SelectedIndexChanged += (_, _) =>
        {
            ApplyGroupFilter();
            BindRobots();
            UpdateButtons();
        };

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var addGroup = UiTheme.CreateButton("New group", ButtonKind.Secondary);
        addGroup.Click += (_, _) => AddGroup();
        _renameGroupButton.Click += (_, _) => RenameGroup();
        _deleteGroupButton.Click += (_, _) => DeleteGroup();
        UiTheme.FitInCell(addGroup);
        UiTheme.FitInCell(_renameGroupButton);
        UiTheme.FitInCell(_deleteGroupButton);
        addGroup.Margin = new Padding(0, 0, 0, 6);
        _renameGroupButton.Margin = new Padding(0, 0, 0, 6);
        _deleteGroupButton.Margin = new Padding(0);
        buttons.Controls.Add(addGroup, 0, 0);
        buttons.Controls.Add(_renameGroupButton, 0, 1);
        buttons.Controls.Add(_deleteGroupButton, 0, 2);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(_groupList, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        return layout;
    }

    private Control BuildRobotPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _listTitle.Dock = DockStyle.Fill;
        _listTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _listTitle.ForeColor = UiTheme.Text;
        _listTitle.TextAlign = ContentAlignment.MiddleLeft;

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(1),
        };
        ConfigureGrid();
        card.Controls.Add(_grid);

        _emptyLabel.Text = "No robots here yet.\nAdd a robot or pick another group.";
        _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.ForeColor = UiTheme.Muted;
        _emptyLabel.BackColor = UiTheme.Surface;
        _emptyLabel.Visible = false;
        card.Controls.Add(_emptyLabel);
        _emptyLabel.BringToFront();

        layout.Controls.Add(_listTitle, 0, 0);
        layout.Controls.Add(card, 0, 1);
        return layout;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        UiTheme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "NAME", FillWeight = 18, MinimumWidth = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = "IP : PORT", FillWeight = 24, MinimumWidth = 170 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Group", HeaderText = "GROUP", FillWeight = 16, MinimumWidth = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "ADB", FillWeight = 16, MinimumWidth = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "NOTES", FillWeight = 26, MinimumWidth = 80 });
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, _) => await RunBusyAsync(() => ConnectAsync(openRemote: true));
        _grid.CellFormatting += Grid_CellFormatting;
    }

    private void ConfigureStatus()
    {
        _status.BackColor = UiTheme.Surface;
        _status.SizingGrip = false;
        _adbStatusLabel.Spring = true;
        _adbStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _lastResultLabel.TextAlign = ContentAlignment.MiddleRight;
        _status.Items.Add(_adbStatusLabel);
        _status.Items.Add(_lastResultLabel);
        _adbStatusLabel.Text = "ADB: checking…";
        _lastResultLabel.Text = string.Empty;
    }

    private void BindGroups()
    {
        var selected = _groupList.SelectedItem as GroupNavItem;
        _groupList.Items.Clear();
        _groupList.Items.Add(new GroupNavItem("All robots", GroupNavKind.All, null, _config.Robots.Count));
        _groupList.Items.Add(new GroupNavItem("Ungrouped", GroupNavKind.Ungrouped, null, CountUngrouped()));

        foreach (var group in _config.Groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var count = _config.Robots.Count(r => r.GroupId == group.Id);
            _groupList.Items.Add(new GroupNavItem(group.Name, GroupNavKind.Group, group.Id, count));
        }

        var match = _groupList.Items.Cast<GroupNavItem>().FirstOrDefault(item =>
            selected is not null
            && item.Kind == selected.Kind
            && item.GroupId == selected.GroupId);
        _groupList.SelectedItem = match ?? _groupList.Items[0];
        ApplyGroupFilter();
    }

    private void ApplyGroupFilter()
    {
        if (_groupList.SelectedItem is not GroupNavItem item)
        {
            _selectedGroupId = null;
            _showUngroupedOnly = false;
            _listTitle.Text = "All robots";
            return;
        }

        _selectedGroupId = item.GroupId;
        _showUngroupedOnly = item.Kind == GroupNavKind.Ungrouped;
        _listTitle.Text = item.Kind switch
        {
            GroupNavKind.All => "All robots",
            GroupNavKind.Ungrouped => "Ungrouped",
            _ => item.Name,
        };
    }

    private IEnumerable<Robot> VisibleRobots()
    {
        if (_showUngroupedOnly)
        {
            return _config.Robots.Where(IsUngrouped);
        }

        if (_selectedGroupId is Guid groupId)
        {
            return _config.Robots.Where(r => r.GroupId == groupId);
        }

        return _config.Robots;
    }

    private bool IsUngrouped(Robot robot)
        => robot.GroupId is null || _config.Groups.All(g => g.Id != robot.GroupId);

    private int CountUngrouped() => _config.Robots.Count(IsUngrouped);

    private string GroupNameFor(Robot robot)
        => _config.Groups.FirstOrDefault(g => g.Id == robot.GroupId)?.Name ?? "Ungrouped";

    private void BindRobots(Guid? selectedId = null)
    {
        selectedId ??= GetSelectedRobot()?.Id;
        _grid.Rows.Clear();

        foreach (var robot in VisibleRobots().OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var status = AdbService.StatusFor(robot, _devices);
            var index = _grid.Rows.Add(robot.Name, robot.Address, GroupNameFor(robot), status, robot.Notes);
            _grid.Rows[index].Tag = robot;
        }

        _emptyLabel.Visible = _grid.Rows.Count == 0;
        _grid.Visible = _grid.Rows.Count > 0;
        if (_grid.Rows.Count == 0)
        {
            UpdateButtons();
            return;
        }

        var match = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is Robot robot && robot.Id == selectedId);
        _grid.ClearSelection();
        if (match is not null)
        {
            match.Selected = true;
            if (match.Cells.Count > 0)
            {
                _grid.CurrentCell = match.Cells[0];
            }
        }
        else
        {
            _grid.Rows[0].Selected = true;
        }
    }

    private Robot? GetSelectedRobot()
        => _grid.CurrentRow?.Tag as Robot ?? _grid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.Tag as Robot;

    private RobotGroup? GetSelectedGroup()
    {
        if (_groupList.SelectedItem is not GroupNavItem { Kind: GroupNavKind.Group, GroupId: Guid id })
        {
            return null;
        }

        return _config.Groups.FirstOrDefault(g => g.Id == id);
    }

    private void SaveConfig() => _store.Save(_config);

    private void AddGroup()
    {
        using var dialog = new GroupEditForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var group = new RobotGroup { Name = dialog.GroupName };
        _config.Groups.Add(group);
        SaveConfig();
        BindGroups();
        SelectGroup(group.Id);
        BindRobots();
        UpdateButtons();
    }

    private void RenameGroup()
    {
        var group = GetSelectedGroup();
        if (group is null)
        {
            return;
        }

        using var dialog = new GroupEditForm(group.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        group.Name = dialog.GroupName;
        SaveConfig();
        BindGroups();
        SelectGroup(group.Id);
        BindRobots();
    }

    private void DeleteGroup()
    {
        var group = GetSelectedGroup();
        if (group is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete group \"{group.Name}\"? Robots in it will move to Ungrouped.",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        foreach (var robot in _config.Robots.Where(r => r.GroupId == group.Id))
        {
            robot.GroupId = null;
        }

        _config.Groups.Remove(group);
        SaveConfig();
        BindGroups();
        BindRobots();
        UpdateButtons();
    }

    private void RevealRobot(Robot robot)
    {
        if (robot.GroupId is Guid groupId)
        {
            SelectGroup(groupId);
            return;
        }

        var ungrouped = _groupList.Items.Cast<GroupNavItem>().FirstOrDefault(i => i.Kind == GroupNavKind.Ungrouped);
        if (ungrouped is not null)
        {
            _groupList.SelectedItem = ungrouped;
        }
    }

    private void SelectGroup(Guid groupId)
    {
        var match = _groupList.Items.Cast<GroupNavItem>().FirstOrDefault(i => i.Kind == GroupNavKind.Group && i.GroupId == groupId);
        if (match is not null)
        {
            _groupList.SelectedItem = match;
        }
    }

    private void AddRobot()
    {
        using var dialog = new RobotEditForm(_config.Groups, defaultGroupId: _selectedGroupId);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.Robots.Add(dialog.Robot);
        SaveConfig();
        BindGroups();
        RevealRobot(dialog.Robot);
        BindRobots(dialog.Robot.Id);
        UpdateButtons();
    }

    private void EditRobot()
    {
        var robot = GetSelectedRobot();
        if (robot is null)
        {
            return;
        }

        using var dialog = new RobotEditForm(_config.Groups, robot);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        robot.Name = dialog.Robot.Name;
        robot.Ip = dialog.Robot.Ip;
        robot.Port = dialog.Robot.Port;
        robot.Notes = dialog.Robot.Notes;
        robot.GroupId = dialog.Robot.GroupId;
        SaveConfig();
        BindGroups();
        RevealRobot(robot);
        BindRobots(robot.Id);
        UpdateButtons();
    }

    private void DeleteRobot()
    {
        var robot = GetSelectedRobot();
        if (robot is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete robot \"{robot.Name}\"?",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _config.Robots.Remove(robot);
        SaveConfig();
        BindGroups();
        BindRobots();
        UpdateButtons();
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_config);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.AdbPath = dialog.AdbPath;
        _config.RemoteDesktopExePath = dialog.RemoteDesktopExePath;
        SaveConfig();
        _ = RefreshDevicesAsync(showErrors: true);
    }

    private async Task ConnectAsync(bool openRemote)
    {
        var robot = GetSelectedRobot();
        if (robot is null)
        {
            return;
        }

        SetLastResult($"Connecting to {robot.Address}…");
        var result = await _adb.ConnectAsync(robot);
        await RefreshDevicesAsync(showErrors: false);

        if (!result.Success)
        {
            SetLastResult(result.Combined);
            MessageBox.Show(this, result.Combined, "ADB connect failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetLastResult(result.Combined);
        BindRobots(robot.Id);

        if (openRemote)
        {
            OpenRemoteDesktop(warnIfDisconnected: false);
        }
    }

    private async Task DisconnectAllAsync()
    {
        SetLastResult("Disconnecting ADB…");
        var result = await _adb.DisconnectAllAsync();
        await RefreshDevicesAsync(showErrors: false);
        SetLastResult(string.IsNullOrWhiteSpace(result.Combined) ? "Disconnected all ADB sessions." : result.Combined);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Combined, "ADB disconnect failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenRemoteDesktop(bool warnIfDisconnected = true)
    {
        var robot = GetSelectedRobot();
        if (warnIfDisconnected && robot is not null && !AdbService.IsConnected(robot, _devices))
        {
            var proceed = MessageBox.Show(
                this,
                "This robot is not currently connected via ADB. Open remote desktop anyway?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (proceed != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            _remoteDesktop.Launch();
            SetLastResult("Remote desktop launched.");
        }
        catch (Exception ex)
        {
            SetLastResult(ex.Message);
            MessageBox.Show(this, ex.Message, "Remote desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RefreshDevicesAsync(bool showErrors)
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            _devices = await _adb.GetDevicesAsync();
            var connected = _devices.Where(d => d.IsReady).Select(d => d.Serial).ToArray();
            _adbStatusLabel.Text = connected.Length == 0
                ? "ADB: no devices connected"
                : $"ADB: {string.Join(", ", connected)}";
            UpdateConnectionLabel();
            BindRobots();
            BindGroups();
            UpdateButtons();
        }
        catch (Exception ex)
        {
            _devices = [];
            _adbStatusLabel.Text = $"ADB: {ex.Message}";
            UpdateConnectionLabel();
            BindRobots();
            UpdateButtons();
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "ADB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateConnectionLabel()
    {
        var connected = _devices.Where(d => d.IsReady).ToList();
        if (connected.Count == 0)
        {
            _connectionLabel.Text = "Not connected";
            _connectionLabel.ForeColor = UiTheme.HeaderMuted;
            return;
        }

        var names = connected.Select(device =>
            _config.Robots.FirstOrDefault(r => string.Equals(r.Address, device.Serial, StringComparison.OrdinalIgnoreCase))?.Name
            ?? device.Serial);
        _connectionLabel.Text = "Connected · " + string.Join(", ", names);
        _connectionLabel.ForeColor = Color.FromArgb(134, 239, 172);
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        UpdateButtons();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetLastResult(ex.Message);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        var robot = GetSelectedRobot();
        var hasSelection = robot is not null && !_busy;
        var hasGroup = GetSelectedGroup() is not null && !_busy;

        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _connectButton.Enabled = hasSelection;
        _disconnectButton.Enabled = !_busy;
        _openRemoteButton.Enabled = !_busy;
        _connectAndOpenButton.Enabled = hasSelection;
        _renameGroupButton.Enabled = hasGroup;
        _deleteGroupButton.Enabled = hasGroup;
    }

    private void SetLastResult(string message)
    {
        var compact = message.ReplaceLineEndings(" ").Trim();
        if (compact.Length > 140)
        {
            compact = compact[..137] + "…";
        }

        _lastResultLabel.Text = compact;
    }

    private void GroupList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _groupList.Items.Count)
        {
            return;
        }

        var item = (GroupNavItem)_groupList.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected ? UiTheme.SidebarSelected : UiTheme.Sidebar;
        using var back = new SolidBrush(background);
        e.Graphics.FillRectangle(back, e.Bounds);

        var nameColor = selected ? UiTheme.Accent : UiTheme.Text;
        var countColor = UiTheme.Muted;
        var nameRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 48, e.Bounds.Height);
        var countRect = new Rectangle(e.Bounds.Right - 40, e.Bounds.Y, 32, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, item.Name, UiTheme.UiFont, nameRect, nameColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, item.Count.ToString(), UiTheme.UiFont, countRect, countColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grid.Columns[e.ColumnIndex].Name != "Status")
        {
            return;
        }

        var status = e.Value?.ToString();
        e.CellStyle.Font = UiTheme.StatusFont;
        e.CellStyle.ForeColor = status switch
        {
            "Connected" => UiTheme.Success,
            "Offline" or "Unauthorized" => UiTheme.Danger,
            _ => UiTheme.Muted,
        };

        if (status == "Connected" && _grid.Rows[e.RowIndex].InheritedStyle is not null)
        {
            _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = UiTheme.ConnectedRow;
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (e.KeyCode == Keys.Delete && _grid.Focused)
        {
            DeleteRobot();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _grid.Focused)
        {
            _ = RunBusyAsync(() => ConnectAsync(openRemote: true));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            _ = RefreshDevicesAsync(showErrors: true);
            e.Handled = true;
        }
    }

    private enum GroupNavKind
    {
        All,
        Ungrouped,
        Group,
    }

    private sealed record GroupNavItem(string Name, GroupNavKind Kind, Guid? GroupId, int Count)
    {
        public override string ToString() => Name;
    }
}
