namespace PuduRobotManager.Ui;

internal enum ButtonKind
{
    Primary,
    Secondary,
    Danger,
    Ghost,
    Header,
}

internal static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(238, 241, 246);
    public static readonly Color Surface = Color.White;
    public static readonly Color Header = Color.FromArgb(17, 27, 46);
    public static readonly Color HeaderMuted = Color.FromArgb(148, 163, 184);
    public static readonly Color Accent = Color.FromArgb(234, 88, 12);
    public static readonly Color AccentHover = Color.FromArgb(249, 115, 22);
    public static readonly Color Secondary = Color.FromArgb(30, 64, 99);
    public static readonly Color SecondaryHover = Color.FromArgb(37, 80, 122);
    public static readonly Color Danger = Color.FromArgb(185, 28, 28);
    public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);
    public static readonly Color Success = Color.FromArgb(4, 120, 87);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(203, 213, 225);
    public static readonly Color GridHeader = Color.FromArgb(248, 250, 252);
    public static readonly Color GridLine = Color.FromArgb(226, 232, 240);
    public static readonly Color ConnectedRow = Color.FromArgb(236, 253, 245);
    public static readonly Color Sidebar = Color.FromArgb(248, 250, 252);
    public static readonly Color SidebarSelected = Color.FromArgb(255, 237, 213);

    public static Font UiFont { get; } = new("Segoe UI", 9.75f);
    public static Font TitleFont { get; } = new("Segoe UI", 14f, FontStyle.Bold);
    public static Font SectionFont { get; } = new("Segoe UI", 8.5f, FontStyle.Bold);
    public static Font StatusFont { get; } = new("Segoe UI", 9f, FontStyle.Bold);
    public static Font ButtonFont { get; } = new("Segoe UI", 9.25f, FontStyle.Bold);

    public static void ApplyForm(Form form)
    {
        form.Font = UiFont;
        form.BackColor = Background;
        form.ForeColor = Text;
    }

    public static Button CreateButton(string text, ButtonKind kind)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(88, 32),
            Padding = new Padding(12, 0, 12, 0),
            Font = ButtonFont,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 8, 0),
        };
        button.FlatAppearance.BorderSize = kind == ButtonKind.Ghost ? 0 : 1;
        ApplyButtonColors(button, kind, hovered: false);

        button.MouseEnter += (_, _) => ApplyButtonColors(button, kind, hovered: true);
        button.MouseLeave += (_, _) => ApplyButtonColors(button, kind, hovered: false);
        button.EnabledChanged += (_, _) => ApplyButtonColors(button, kind, hovered: false);
        return button;
    }

    public static void FitInCell(Button button)
    {
        button.AutoSize = false;
        button.Dock = DockStyle.Fill;
        button.Padding = new Padding(8, 0, 8, 0);
        button.Margin = new Padding(0);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.MinimumSize = new Size(0, 32);
    }

    public static void StyleTextBox(TextBox box)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Surface;
        box.ForeColor = Text;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Surface;
        grid.GridColor = GridLine;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 34;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridHeader,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            SelectionBackColor = GridHeader,
            SelectionForeColor = Muted,
            Padding = new Padding(8, 0, 8, 0),
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = Text,
            SelectionBackColor = Color.FromArgb(255, 247, 237),
            SelectionForeColor = Text,
            Padding = new Padding(8, 0, 8, 0),
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Text,
            SelectionBackColor = Color.FromArgb(255, 247, 237),
            SelectionForeColor = Text,
            Padding = new Padding(8, 0, 8, 0),
        };

        typeof(DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(grid, true);
    }

    private static void ApplyButtonColors(Button button, ButtonKind kind, bool hovered)
    {
        if (!button.Enabled)
        {
            button.BackColor = Color.FromArgb(226, 232, 240);
            button.ForeColor = Color.FromArgb(148, 163, 184);
            button.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            return;
        }

        switch (kind)
        {
            case ButtonKind.Primary:
                button.BackColor = hovered ? AccentHover : Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = button.BackColor;
                break;
            case ButtonKind.Danger:
                button.BackColor = hovered ? DangerHover : Danger;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = button.BackColor;
                break;
            case ButtonKind.Ghost:
                button.BackColor = hovered ? Color.FromArgb(241, 245, 249) : Sidebar;
                button.ForeColor = Text;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.BorderColor = button.BackColor;
                break;
            case ButtonKind.Header:
                button.BackColor = hovered ? Color.FromArgb(30, 41, 59) : Header;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = hovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(51, 65, 85);
                break;
            default:
                button.BackColor = hovered ? Color.FromArgb(241, 245, 249) : Surface;
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                break;
        }
    }
}
