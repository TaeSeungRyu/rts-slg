namespace SanguoSLG.Game;

using Godot;
using System.Collections.Generic;

public static class ActiveSkillIcons
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D? Load(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) { return null; }
        if (Cache.TryGetValue(code, out var texture)) { return texture; }
        var path = $"res://assets/icons/skills/{code}.png";
        if (!ResourceLoader.Exists(path)) { return null; }
        texture = GD.Load<Texture2D>(path);
        Cache[code] = texture;
        return texture;
    }

    public static void Apply(Button button, string? code, int size = 44)
    {
        button.Icon = Load(code);
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("icon_max_width", size);
        button.AddThemeConstantOverride("h_separation", 12);
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, size + 12);
    }

    public static TextureRect Preview(string? code, int size = 80) => new()
    {
        Texture = Load(code),
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        TextureFilter = CanvasItem.TextureFilterEnum.Linear,
    };
}
