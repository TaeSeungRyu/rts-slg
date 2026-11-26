namespace SanguoSLG.Game;

using Godot;

/// <summary>턴 진행 UI의 화면 위치, 크기, 색상을 한곳에서 조정한다.</summary>
internal static class TurnProgressLayout
{
    internal const float PanelWidth = 190f;
    internal const float ScreenInset = 16f;
    internal const int PanelPadding = 5;
    internal const int RowGap = 3;
    internal const int DayFontSize = 18;
    internal const int TurnFontSize = 13;
    internal const int ButtonFontSize = 17;
    internal const float ButtonWidth = 176f;
    internal const float ButtonHeight = 43f;
    internal const float ProgressInset = 20f;
    internal const float ProgressTop = 8f;
    internal const float ProgressHeight = 8f;
    internal const int ProgressRadius = 4;
    internal static readonly Color BodyColor = new("#24150f");
    internal static readonly Color Gold = new("#dda94d");
    internal static readonly Color ButtonText = new("#ffda87");
    internal static readonly Color DisabledText = new("#a69070");
    internal static readonly Color DisabledTint = new(0.58f, 0.53f, 0.49f);
    internal static readonly Color TextOutline = new("#140904");
    internal static readonly Color ProgressTrack = new("#24150f");
    internal static readonly Color ProgressFill = new("#ffc252");
}
