namespace SanguoSLG.Game;

using Godot;

/// <summary>턴 진행 UI의 화면 위치, 크기, 색상을 한곳에서 조정한다.</summary>
internal static class TurnProgressLayout
{
    internal const float PanelWidth = 160f;
    internal const float PanelHeight = 64f;
    internal const float ScreenInset = 16f;
    internal const int PanelPadding = 8;
    internal const int FramePatchMargin = 18;
    internal const float FrameScale = 0.10f;
    internal const int RowGap = 3;
    internal const int DayFontSize = 20;
    internal const int TurnFontSize = 15;
    internal const int StatusShadowOffset = 2;
    internal static readonly Color StatusShadow = new(0.78f, 0.80f, 0.82f, 0.7f);
    internal const int ButtonFontSize = 14;
    internal const float ButtonWidth = 141f;
    internal const float ButtonHeight = 34f;
    internal const float ProgressInset = 20f;
    internal const float ProgressTop = 8f;
    internal const float ProgressHeight = 8f;
    internal const float ProgressWidthRatio = 0.8f;
    internal const int ProgressExtraGap = 5;
    internal const int ProgressRadius = 4;
    internal static readonly Color BodyColor = new("#24150f");
    internal static readonly Color Gold = new("#dda94d");
    internal static readonly Color ButtonText = new("#ffda87");
    internal static readonly Color DisabledText = new("#a69070");
    internal static readonly Color DisabledTint = new(0.82f, 0.76f, 0.68f);
    internal static readonly Color TextOutline = new("#140904");
    internal static readonly Color ProgressTrack = new("#24150f");
    internal static readonly Color ProgressFill = new("#ffc252");
}
