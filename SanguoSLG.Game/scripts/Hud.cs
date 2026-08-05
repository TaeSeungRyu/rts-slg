using System;
using System.Linq;
using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 화면 고정 HUD. 상단 상태바(날짜·세력 자금·다음 달)와 하단 정보 패널.
/// 이미지 아트 없이 StyleBox·폰트·레이아웃만으로 구성한다.
/// </summary>
public partial class Hud : CanvasLayer
{
    public event Action? NextMonthPressed;

    private static readonly Color PanelBg = new(0.10f, 0.11f, 0.13f, 0.94f);
    private static readonly Color Accent = new(0.82f, 0.68f, 0.38f);
    private static readonly Color TextColor = new(0.90f, 0.92f, 0.95f);

    private Font _font = null!;
    private Label _dateLabel = null!;
    private Label _resourceLabel = null!;
    private Label _infoLabel = null!;

    public override void _Ready()
    {
        _font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        BuildTopBar();
        BuildInfoPanel();
    }

    public void SetState(GameState state)
    {
        _dateLabel.Text = $"{state.Year}년 {state.Month}월";
        _resourceLabel.Text = string.Join("      ",
            state.Factions.OrderBy(f => f.Id.Value).Select(f => $"{f.Name}  자금 {f.Gold}"));
    }

    public void ShowInfo(string text) => _infoLabel.Text = text;

    private void BuildTopBar()
    {
        var bar = new PanelContainer { AnchorRight = 1f, OffsetBottom = 52f };
        bar.AddThemeStyleboxOverride("panel", MakeStyle(borderAll: false));
        AddChild(bar);

        var margin = MakeMargin(18, 8);
        bar.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        margin.AddChild(row);

        _dateLabel = MakeLabel("1년 1월", 20, Accent);
        _dateLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_dateLabel);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _resourceLabel = MakeLabel(string.Empty, 16, TextColor);
        _resourceLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_resourceLabel);

        var button = new Button { Text = "다음 달 ▶" };
        StyleButton(button);
        button.Pressed += () => NextMonthPressed?.Invoke();
        row.AddChild(button);
    }

    private void BuildInfoPanel()
    {
        var panel = new PanelContainer
        {
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 16f,
            OffsetRight = 304f,
            OffsetTop = -172f,
            OffsetBottom = -16f,
        };
        panel.AddThemeStyleboxOverride("panel", MakeStyle(borderAll: true));
        AddChild(panel);

        var margin = MakeMargin(14, 14);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        box.AddChild(MakeLabel("정보", 17, Accent));

        _infoLabel = MakeLabel("도시나 부대를 클릭해 선택하세요.", 15, TextColor);
        _infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_infoLabel);

        box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var hint = MakeLabel("좌클릭 이동 · 휠 줌 · 우클릭 드래그/WASD 팬 · Q/E 회전", 12, new Color(0.62f, 0.64f, 0.68f));
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(hint);
    }

    private Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static MarginContainer MakeMargin(int horizontal, int vertical)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", horizontal);
        margin.AddThemeConstantOverride("margin_right", horizontal);
        margin.AddThemeConstantOverride("margin_top", vertical);
        margin.AddThemeConstantOverride("margin_bottom", vertical);
        return margin;
    }

    private void StyleButton(Button button)
    {
        button.AddThemeFontOverride("font", _font);
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", new Color(0.12f, 0.12f, 0.14f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.10f, 0.10f, 0.12f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.10f, 0.10f, 0.12f));

        var normal = ButtonStyle(Accent);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.92f, 0.78f, 0.48f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.70f, 0.57f, 0.30f)));
    }

    private static StyleBoxFlat ButtonStyle(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 16,
        ContentMarginRight = 16,
        ContentMarginTop = 7,
        ContentMarginBottom = 7,
    };

    private static StyleBoxFlat MakeStyle(bool borderAll)
    {
        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = Accent,
            BorderWidthBottom = 2,
        };

        if (borderAll)
        {
            style.BorderWidthTop = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
        }

        return style;
    }
}
