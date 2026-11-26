namespace SanguoSLG.Game;

using Godot;

internal static class MerchantModalLayout
{
    public const float Width = 1080f;
    public const float Height = 760f;
    public const float ViewportMargin = 32f;
    public const float LeftShare = 0.30f;
    public const float ColumnGap = 16f;
    public const float CardGap = 12f;
    public const float ScrollContentGap = 8f;
    public const float PortraitHeight = 265f;
    public const float FooterButtonWidth = 180f;
    public const int TitleSize = 23;
    public const int SectionSize = 18;
    public const int BodySize = 14;
    public const string AssetRoot = "res://assets/ui/game/merchant/";

    public static readonly Color Panel = new("21130e");
    public static readonly Color Card = new("2a1811");
    public static readonly Color Selected = new("6e1d14");
    public static readonly Color Hover = new("493024");
    public static readonly Color Gold = new("d9a84e");
    public static readonly Color Green = new("9dcf7b");
}
