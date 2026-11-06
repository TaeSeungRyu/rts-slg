using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Application.EnableVisualStyles();

if (args.Length >= 4 && args[0] == "--edit")
{
    var multiline = args[1] == "multi";
    var initial = Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
    using var form = new Form
    {
        Text = "텍스트 입력",
        StartPosition = FormStartPosition.CenterScreen,
        ClientSize = new Size(660, multiline ? 320 : 175),
        FormBorderStyle = FormBorderStyle.FixedDialog,
        MaximizeBox = false,
        MinimizeBox = false,
        BackColor = Color.FromArgb(23, 14, 12),
        ForeColor = Color.FromArgb(239, 220, 184),
        Font = new Font("Malgun Gothic", 10)
    };
    var gold = Color.FromArgb(181, 136, 60);
    var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3 };
    body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
    body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    body.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
    body.Controls.Add(new Label
    {
        Text = multiline ? "설명 입력" : "이름·제목 입력", ForeColor = gold,
        Font = new Font("Malgun Gothic", 12, FontStyle.Bold), Dock = DockStyle.Fill
    }, 0, 0);
    var frame = new Panel { Dock = DockStyle.Fill, BackColor = gold, Padding = new Padding(1) };
    var input = new TextBox
    {
        Dock = DockStyle.Fill, Text = initial, Multiline = true, AcceptsReturn = multiline,
        BackColor = Color.FromArgb(41, 27, 23), ForeColor = Color.FromArgb(239, 220, 184),
        BorderStyle = BorderStyle.None, Font = new Font("Malgun Gothic", 13),
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
    };
    frame.Controls.Add(input);
    body.Controls.Add(frame, 0, 1);
    var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
    Button Action(string label, Color background)
    {
        var button = new Button
        {
            Text = label, Size = new Size(94, 34), BackColor = background,
            ForeColor = Color.FromArgb(239, 220, 184), FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, Margin = new Padding(8, 3, 0, 0)
        };
        button.FlatAppearance.BorderColor = gold;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(103, 44, 27);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(54, 23, 19);
        return button;
    }
    var confirm = Action("확인", Color.FromArgb(91, 29, 23));
    var cancel = Action("취소", Color.FromArgb(41, 27, 23));
    confirm.Click += (_, _) => { File.WriteAllText(args[3], input.Text, Encoding.UTF8); form.Close(); };
    cancel.Click += (_, _) => form.Close();
    actions.Controls.Add(confirm);
    actions.Controls.Add(cancel);
    body.Controls.Add(actions, 0, 2);
    form.Controls.Add(body);
    form.HandleCreated += (_, _) => WindowChrome.Apply(form.Handle);
    form.AcceptButton = multiline ? null : confirm;
    form.CancelButton = cancel;
    form.Shown += (_, _) => { input.Focus(); input.SelectionStart = input.TextLength; };
    if (args.Length >= 6 && args[4] == "--parent" && int.TryParse(args[5], out var parentId))
    {
        var lifetime = new System.Windows.Forms.Timer { Interval = 300 };
        lifetime.Tick += (_, _) =>
        {
            try
            {
                using var parent = Process.GetProcessById(parentId);
                if (parent.HasExited)
                    form.Close();
            }
            catch (ArgumentException)
            {
                form.Close();
            }
        };
        form.FormClosed += (_, _) => lifetime.Dispose();
        lifetime.Start();
    }
    Application.Run(form);
}

else
{
    using var form = new Form { Text = "한글 입력 진단", StartPosition = FormStartPosition.CenterScreen,
        ClientSize = new Size(640, 320) };
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 4 };
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    var single = new TextBox { Dock = DockStyle.Fill, Font = new Font("Malgun Gothic", 13) };
    var multi = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Malgun Gothic", 13) };
    layout.Controls.Add(new Label { Text = "한 줄 입력: 안녕하세요" }, 0, 0);
    layout.Controls.Add(single, 0, 1);
    layout.Controls.Add(new Label { Text = "여러 줄 입력: 안녕하세요" }, 0, 2);
    layout.Controls.Add(multi, 0, 3);
    form.Controls.Add(layout);
    form.Shown += (_, _) => single.Focus();
    Application.Run(form);
}

internal static class WindowChrome
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(IntPtr window)
    {
        var enabled = 1;
        var caption = 0x000E171C;
        var text = 0x00B8DCEF;
        DwmSetWindowAttribute(window, 20, ref enabled, sizeof(int));
        DwmSetWindowAttribute(window, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(window, 36, ref text, sizeof(int));
    }
}
