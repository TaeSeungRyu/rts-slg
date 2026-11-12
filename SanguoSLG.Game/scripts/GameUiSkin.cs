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
        SetCenteredIcon(button, icon);
    }

    internal static void SetCenteredIcon(Button button, Texture2D icon)
    {
        button.Text = "";
        button.Icon = null;
        button.Alignment = HorizontalAlignment.Center;
        var existing = button.GetNodeOrNull<TextureRect>("GameUiCenteredIcon");
        if (existing is not null)
        {
            existing.Texture = icon;
            return;
        }
        var iconView = new TextureRect
        {
            Name = "GameUiCenteredIcon",
            Texture = icon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        iconView.SetAnchorsPreset(Control.LayoutPreset.Center);
        iconView.OffsetLeft = -11.5f;
        iconView.OffsetTop = -11.5f;
        iconView.OffsetRight = 11.5f;
        iconView.OffsetBottom = 11.5f;
        button.AddChild(iconView);
    }

    internal static Texture2D? Icon(string assetName)
    {
        var path = $"{Root}/icons/{assetName}.png";
        if (TryLoad(path, out var texture)) return texture;
        WarnMissing($"icon:{assetName}", path);
        return null;
    }

    internal static Texture2D? ListTexture(string assetName, string? fallbackPath = null)
    {
        var path = $"{Root}/lists/{assetName}.png";
        if (TryLoad(path, out var texture)) return texture;
        if (fallbackPath is not null && TryLoad(fallbackPath, out texture)) return texture;
        WarnMissing($"list:{assetName}", path);
        return null;
    }

    internal static Texture2D? CommandTexture(string fileName)
    {
        var path = $"{Root}/commands/{fileName}";
        if (TryLoad(path, out var texture)) return texture;
        WarnMissing($"command:{fileName}", path);
        return null;
    }

    internal static bool ApplyMainCommandArtwork(CommandPaletteButton button, string menuName, bool accent = false)
    {
        button.SetMeta("command_accent", accent);
        var normal = CommandTexture($"{menuName}normal.png");
        var active = CommandTexture($"{menuName}active.png");
        if (normal is null || active is null) return false;
        button.Configure(normal, active, fitArtworkWidth: true);
        return true;
    }

    internal static bool ApplySubCommandArtwork(CommandPaletteButton button)
    {
        var normal = CommandTexture("서브normal.png");
        var active = CommandTexture("서브active.png");
        var disabled = CommandTexture("서브disabled.png");
        if (normal is null || active is null || disabled is null) return false;
        button.Configure(normal, active, disabled, 0.08f, 0.08f);
        return true;
    }

    internal static StyleBoxTexture? CommandFrameBox(string fileName, int horizontalMargin, int verticalMargin)
    {
        var texture = CommandTexture(fileName);
        if (texture is null) return null;
        return new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = horizontalMargin,
            TextureMarginRight = horizontalMargin,
            TextureMarginTop = verticalMargin,
            TextureMarginBottom = verticalMargin,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
    }

    internal static StyleBox QuickListPanel()
    {
        var path = $"{Root}/lists/list_body.png";
        if (TryLoad(path, out var texture)) return TextureBox(texture, 28, 12);
        return FlatBox(new Color("160d0b", 0.96f), new Color("b9873e"), 2, 8, 10);
    }

    internal static void ApplyQuickListHeader(Button button)
    {
        var path = $"{Root}/lists/list_header.png";
        if (TryLoad(path, out var texture))
        {
            button.AddThemeStyleboxOverride("normal", TextureBox(texture, 24, 10));
            button.AddThemeStyleboxOverride("hover", TextureBox(texture, 24, 10));
            button.AddThemeStyleboxOverride("pressed", TextureBox(texture, 24, 10));
        }
        else
        {
            button.AddThemeStyleboxOverride("normal", FlatBox(new Color("2a1511", 0.94f),
                new Color("8d6334"), 1, 6, 9));
            button.AddThemeStyleboxOverride("hover", FlatBox(new Color("3a2018", 0.98f),
                GoldBright, 1, 6, 9));
            button.AddThemeStyleboxOverride("pressed", FlatBox(new Color("1b0d0b"),
                Gold, 1, 6, 9));
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ivory);
        button.AddThemeColorOverride("font_hover_color", new Color("fff0c6"));
        button.AddThemeColorOverride("font_pressed_color", GoldBright);
    }

    internal static void ApplyQuickListRow(Button button)
    {
        var hover = ListTexture("list_hover");
        var selected = ListTexture("list_selected");
        button.AddThemeStyleboxOverride("normal", FlatBox(new Color("1b100e", 0.82f),
            new Color(Bronze, 0.45f), 1, 5, 7));
        button.AddThemeStyleboxOverride("hover", hover is not null
            ? TextureBox(hover, 18, 7)
            : FlatBox(new Color("3a241a", 0.96f), new Color(Gold, 0.78f), 1, 5, 7));
        button.AddThemeStyleboxOverride("pressed", selected is not null
            ? TextureBox(selected, 18, 7)
            : FlatBox(new Color("593018", 0.99f), GoldBright, 2, 5, 7));
        button.AddThemeStyleboxOverride("hover_pressed", selected is not null
            ? TextureBox(selected, 18, 7)
            : FlatBox(new Color("6b3b1d", 1f), new Color("ffe09a"), 2, 5, 7));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ivory);
        button.AddThemeColorOverride("font_hover_color", new Color("fff0c6"));
        button.AddThemeColorOverride("font_pressed_color", GoldBright);
    }

    internal static void ApplyQuickListScroll(ScrollContainer scroll)
    {
        scroll.AddThemeConstantOverride("scrollbar_v_separation", 10);
        var bar = scroll.GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(7, 0);
        bar.AddThemeStyleboxOverride("scroll", FlatBox(new Color("241713"),
            new Color("3e2b20"), 1, 3, 0));
        bar.AddThemeStyleboxOverride("grabber", FlatBox(new Color("8c683b"),
            new Color("b78a4a"), 1, 3, 0));
        bar.AddThemeStyleboxOverride("grabber_highlight", FlatBox(new Color("c6a05b"),
            GoldBright, 1, 3, 0));
        bar.AddThemeStyleboxOverride("grabber_pressed", FlatBox(new Color("a87c3f"),
            new Color("d9b66e"), 1, 3, 0));
    }

    internal static void ApplyCommandButton(Button button, bool accent = false)
    {
        // Accent marks the two primary actions (deploy/production); pressed state alone marks selection.
        button.SetMeta("command_accent", accent);
        button.AddThemeStyleboxOverride("normal", FlatBox(
            accent ? new Color("542018", 0.98f) : new Color("241411", 0.96f),
            new Color("9d7139"), 1, 6, 8));
        button.AddThemeStyleboxOverride("hover", FlatBox(
            accent ? new Color("743025") : new Color("3b241a"), GoldBright, 2, 6, 8));
        button.AddThemeStyleboxOverride("pressed", FlatBox(
            new Color("6b281e"), GoldBright, 2, 6, 8));
        button.AddThemeStyleboxOverride("hover_pressed", FlatBox(
            new Color("843326"), new Color("ffe09a"), 2, 6, 8));
        button.AddThemeStyleboxOverride("disabled", FlatBox(
            new Color("211715", 0.82f), new Color(Gold, 0.30f), 1, 6, 8));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ivory);
        button.AddThemeColorOverride("font_hover_color", new Color("fff0c6"));
        button.AddThemeColorOverride("font_pressed_color", GoldBright);
        button.AddThemeColorOverride("font_disabled_color", new Color(Ivory, 0.42f));
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
