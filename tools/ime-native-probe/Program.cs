using System;
using System.Drawing;
using System.Windows.Forms;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Application.EnableVisualStyles();

using var form = new Form
{
    Text = "한글 입력 진단 — Windows 기본 입력창",
    StartPosition = FormStartPosition.CenterScreen,
    ClientSize = new Size(640, 350),
    MinimumSize = new Size(480, 300)
};
var layout = new TableLayoutPanel
{
    Dock = DockStyle.Fill,
    Padding = new Padding(16),
    ColumnCount = 1,
    RowCount = 5
};
layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

var single = new TextBox { Dock = DockStyle.Fill, Font = new Font("Malgun Gothic", 13) };
var multi = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
    Font = new Font("Malgun Gothic", 13) };
var result = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
layout.Controls.Add(new Label { Text = "한 줄 입력: 안녕하세요", Dock = DockStyle.Fill }, 0, 0);
layout.Controls.Add(single, 0, 1);
layout.Controls.Add(new Label { Text = "여러 줄 입력: 안녕하세요", Dock = DockStyle.Fill }, 0, 2);
layout.Controls.Add(multi, 0, 3);
layout.Controls.Add(result, 0, 4);
form.Controls.Add(layout);
single.TextChanged += (_, _) => result.Text = $"한 줄 결과: {single.Text}";
form.Shown += (_, _) => single.Focus();
Application.Run(form);
