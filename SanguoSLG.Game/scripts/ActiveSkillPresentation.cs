using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>검수장과 캠페인이 함께 쓰는 액티브 발동 표현 진입점.</summary>
public static class ActiveSkillPresentation
{
    public static void ShowCasterActivation(Node3D caster)
    {
        // Burst는 범용 EffectView에서 반복형 파티클이므로 loop 인자만으로는 스스로 사라지지 않는다.
        // 발동 순간에만 보이도록 효과 루트에 수명 타이머를 붙여 확실히 정리한다.
        var effect = EffectView.Attach(caster, EffectKind.Burst, 0.72f, loop: false);
        var lifetime = new Godot.Timer { OneShot = true, WaitTime = 0.55 };
        effect.AddChild(lifetime);
        lifetime.Timeout += effect.QueueFree;
        lifetime.Start();
    }

    public static bool AttachEffect(Node3D target, ActiveSkill skill)
    {
        if (skill.Code == "fire_plot")
        {
            EffectView.Attach(target, EffectKind.Fire, 0.9f, loop: false);
            return true;
        }
        if (skill.Code == "peerless")
        {
            target.AddChild(new PeerlessCloudEffectView3D());
            return true;
        }
        if (skill.Code == "one_man_army")
        {
            target.AddChild(new OneManArmySwordEffectView3D());
            return true;
        }
        return false;
    }

    public static void ShowBanner(Node owner, string generalName, ActiveSkill skill, Texture2D? portrait = null)
    {
        var layer = new CanvasLayer { Layer = 90 };
        owner.AddChild(layer);
        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.12f,
            AnchorBottom = 0.12f,
            OffsetLeft = -220,
            OffsetRight = 220,
            OffsetBottom = 86,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.045f, 0.035f, 0.95f),
            BorderColor = new Color(0.96f, 0.72f, 0.25f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        });
        layer.AddChild(panel);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        panel.AddChild(row);
        if (portrait is not null)
        {
            row.AddChild(new TextureRect
            {
                Texture = portrait,
                CustomMinimumSize = new Vector2(68, 68),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            });
        }
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        var name = new Label { Text = generalName, HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 16);
        name.AddThemeColorOverride("font_color", new Color(0.94f, 0.86f, 0.69f));
        text.AddChild(name);
        var skillName = new Label { Text = $"「 {skill.Name} 」", HorizontalAlignment = HorizontalAlignment.Center };
        skillName.AddThemeFontSizeOverride("font_size", 28);
        skillName.AddThemeColorOverride("font_color", new Color(1f, 0.72f, 0.24f));
        text.AddChild(skillName);

        var tween = owner.CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.16f);
        tween.TweenInterval(1.15f);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.28f);
        tween.Finished += layer.QueueFree;
    }
}
