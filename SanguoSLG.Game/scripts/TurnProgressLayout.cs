namespace SanguoSLG.Game;

using Godot;

/// <summary>우하단 진행 UI의 위치, 크기, 여백을 조정한다.</summary>
internal static class TurnProgressLayout
{
    internal const string AssetRoot = "res://assets/ui/game/turn_progress/";
    internal const float ScreenInset = 20f;
    internal const float ButtonWidth = 170f, ButtonHeight = 46f;
    internal const float StatusWidth = 200f, StatusHeight = 168f;
    internal const int StatusHorizontalInset = 25, StatusTopInset = 25, StatusBottomInset = 22;
    internal const int StatusGap = 8;
    internal const int ButtonFontSize = 16, DayFontSize = 20, TurnFontSize = 16, BusyFontSize = 14;
    internal const float ProgressHeight = 10f;
    internal const int ProgressRadius = 5;
    internal static readonly Color ButtonText = new("#ffda87");
    internal static readonly Color TextOutline = new("#140904");
    internal static readonly Color ProgressTrack = new("#24150f");
    internal static readonly Color ProgressFill = new("#ffc252");
}
