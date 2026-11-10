using Godot;

namespace SanguoSLG.Game;

internal static class GameUiSkin
{
    internal static readonly Color Ink = new("160d0b", 0.94f);
    internal static readonly Color InkRaised = new("261511", 0.96f);
    internal static readonly Color Bronze = new("6f4f2d");
    internal static readonly Color Gold = new("c89a4b");
    internal static readonly Color GoldBright = new("f0c873");
    internal static readonly Color Ivory = new("f1dfbd");

    private const string Root = "res://assets/ui/game";
    private static readonly HashSet<string> MissingSets = [];

    internal static StyleBox Panel(string assetName, int slice, int radius, int padding)
    {
        var path = $"{Root}/panels/{assetName}.png";
        if (TryLoad(path, out var texture)) return TextureBox(texture, slice, padding);
        WarnMissing($"panel:{assetName}", path);
        return FlatBox(Ink, Gold, 1, radius, padding);
    }

    internal static StyleBox Panel(string assetName, int leftSlice, int topSlice, int rightSlice,
        int bottomSlice, int radius, int padding)
    {
        var path = $"{Root}/panels/{assetName}.png";
        if (TryLoad(path, out var texture))
            return TextureBox(texture, leftSlice, topSlice, rightSlice, bottomSlice, padding);
        WarnMissing($"panel:{assetName}", path);
        return FlatBox(Ink, Gold, 1, radius, padding);
    }

    internal static StyleBox PortraitFrame(string assetName, int slice, int padding)
    {
        var path = $"{Root}/frames/{assetName}.png";
        if (TryLoad(path, out var texture)) return TextureBox(texture, slice, padding);
        WarnMissing($"frame:{assetName}", path);
        return FlatBox(new Color("24150f"), GoldBright, 3, 64, padding);
    }

    internal static Texture2D? PortraitFrameTexture(string assetName)
    {
        var path = $"{Root}/frames/{assetName}.png";
        if (TryLoad(path, out var texture)) return texture;
        WarnMissing($"frame:{assetName}", path);
        return null;
    }

    internal static void ApplyButton(Button button, string assetName, bool accent = false, int textureSlice = 18)
    {
        var states = new[] { "normal", "hover", "pressed", "disabled" };
        var textures = states.Select(state =>
        {
            var path = $"{Root}/buttons/{assetName}_{state}.png";
            return TryLoad(path, out var texture) ? texture : null;
        }).ToArray();
        if (textures.All(texture => texture is not null))
        {
            for (var index = 0; index < states.Length; index++)
                button.AddThemeStyleboxOverride(states[index], TextureBox(textures[index]!, textureSlice, 8));
        }
        else
        {
            WarnMissing($"button:{assetName}", $"{Root}/buttons/{assetName}_{{normal,hover,pressed,disabled}}.png");
            button.AddThemeStyleboxOverride("normal", FlatBox(accent ? new Color("61251c") : InkRaised,
                Gold, 1, 7, 8));
            button.AddThemeStyleboxOverride("hover", FlatBox(accent ? new Color("873328") : new Color("3b241a"),
                GoldBright, 2, 7, 8));
            button.AddThemeStyleboxOverride("pressed", FlatBox(accent ? new Color("461713") : new Color("190f0d"),
                Gold, 1, 7, 8));
            button.AddThemeStyleboxOverride("disabled", FlatBox(new Color("211917"),
                new Color(Gold, 0.35f), 1, 7, 8));
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ivory);
        button.AddThemeColorOverride("font_hover_color", new Color("fff0c6"));
        button.AddThemeColorOverride("font_pressed_color", GoldBright);
        button.AddThemeColorOverride("font_disabled_color", new Color(Ivory, 0.45f));
    }

    internal static void ApplyIconButton(Button button, string buttonAssetName, string iconAssetName)
    {
        ApplyButton(button, buttonAssetName, textureSlice: 0);
        if (Icon(iconAssetName) is not { } icon) return;
        button.Text = "";
        button.Icon = icon;
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 23);
        button.IconAlignment = HorizontalAlignment.Center;
    }

    internal static Texture2D? Icon(string assetName)
    {
        var path = $"{Root}/icons/{assetName}.png";
        if (TryLoad(path, out var texture)) return texture;
        WarnMissing($"icon:{assetName}", path);
        return null;
    }

    internal static StyleBoxFlat FlatBox(Color background, Color border, int borderWidth, int radius, int padding)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(padding);
        return style;
    }

    private static StyleBoxTexture TextureBox(Texture2D texture, int slice, int padding)
    {
        var style = new StyleBoxTexture { Texture = texture };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
        {
            style.SetTextureMargin(side, slice);
            style.SetContentMargin(side, padding);
        }
        return style;
    }

    private static StyleBoxTexture TextureBox(Texture2D texture, int leftSlice, int topSlice,
        int rightSlice, int bottomSlice, int padding)
    {
        var style = new StyleBoxTexture { Texture = texture };
        style.SetTextureMargin(Side.Left, leftSlice);
        style.SetTextureMargin(Side.Top, topSlice);
        style.SetTextureMargin(Side.Right, rightSlice);
        style.SetTextureMargin(Side.Bottom, bottomSlice);
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
            style.SetContentMargin(side, padding);
        return style;
    }

    private static bool TryLoad(string path, out Texture2D texture)
    {
        texture = null!;
        if (ResourceLoader.Exists(path))
        {
            texture = GD.Load<Texture2D>(path);
            if (texture is not null) return true;
        }

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(absolutePath)) return false;
        var image = Image.LoadFromFile(absolutePath);
        if (image is null || image.IsEmpty()) return false;
        texture = ImageTexture.CreateFromImage(image);
        return texture is not null;
    }

    private static void WarnMissing(string set, string path)
    {
        if (!MissingSets.Add(set)) return;
        GD.PushWarning($"[game-ui] 이미지 에셋 없음, 기본 스타일 사용: {path}");
    }
}
