namespace SanguoSLG.Game;

using Godot;

/// <summary>턴 진행 UI의 화면 위치, 크기, 색상을 한곳에서 조정한다.</summary>
internal static class TurnProgressLayout
{
    internal const float PanelWidth = 310f;
    internal const float ScreenInset = 16f;
    internal const int PanelPadding = 8;
    internal const int RowGap = 3;
    internal const int DayFontSize = 23;
    internal const int TurnFontSize = 16;
    internal const int ButtonFontSize = 25;
    internal const float ButtonWidth = 294f;
    internal const float ButtonHeight = 72f;
    internal const float ProgressInset = 20f;
    internal const float ProgressTop = 8f;
    internal const float ProgressHeight = 5f;
    internal static readonly Color BodyColor = new("#24150f");
    internal static readonly Color Gold = new("#dda94d");
    internal static readonly Color ButtonText = new("#ffda87");
    internal static readonly Color DisabledText = new("#a69070");
    internal static readonly Color DisabledTint = new(0.58f, 0.53f, 0.49f);
    internal static readonly Color TextOutline = new("#140904");
    internal static readonly Color ProgressTrack = new("#24150f");
    internal static readonly Color ProgressFill = new("#ffc252");
}
