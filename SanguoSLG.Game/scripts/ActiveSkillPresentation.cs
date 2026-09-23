using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>검수장과 캠페인이 함께 쓰는 액티브 발동 표현 진입점.</summary>
public static class ActiveSkillPresentation
{
    public static bool ShowBreakthrough(Node3D caster, Node3D target)
    {
        if (caster is not UnitController3D unit) return false;
        unit.PlayBreakthroughMotionToward(target.GlobalPosition);
        return true;
    }

    public static bool ShowTigerStrike(Node3D caster, Node3D target)
    {
        var parent = target.GetParent();
        if (parent is null) return false;
        var effect = new TigerStrikeEffectView3D();
        effect.Configure(caster.GlobalPosition, target.GlobalPosition);
        parent.AddChild(effect);
        effect.GlobalPosition = target.GlobalPosition;
        return true;
    }

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

    public static bool AttachEffect(Node3D target, ActiveSkill skill, Vector3? facingPosition = null)
    {
        if (skill.Code == "fire_plot")
        {
            EffectView.Attach(target, EffectKind.Fire, 0.9f, loop: false);
            return true;
        }
        if (skill.Code == "barrage")
        {
            target.AddChild(new PeerlessCloudEffectView3D());
            return true;
        }
        if (skill.Code == "peerless")
        {
            EffectView.Attach(target, EffectKind.Tear, 1f, loop: false);
            return true;
        }
        if (skill.Code == "reap")
        {
            EffectView.Attach(target, EffectKind.Shatter, 1f, loop: false);
            return true;
        }
        if (skill.Code == "one_man_army")
        {
            target.AddChild(new OneManArmySwordEffectView3D());
            return true;
        }
        if (skill.Code == "flash")
        {
            target.AddChild(new FlashSlashEffectView3D());
            return true;
        }
        if (skill.Code == "chain_strike")
        {
            target.AddChild(new ChainStrikeSwordEffectView3D());
            return true;
        }
        if (skill.Code == "armor_break")
        {
            target.AddChild(new ArmorBreakEffectView3D());
            return true;
        }
        if (skill.Code == "heavy_blow")
        {
            target.AddChild(new HeavyBlowExplosionEffectView3D());
            return true;
        }
        if (skill.Code == "double_hit")
        {
            target.AddChild(new DoubleHitImpactEffectView3D());
            return true;
        }
        if (skill.Code == "iron_wall")
        {
            var effect = new IronWallArmorEffectView3D();
            target.AddChild(effect);
            return true;
        }
        if (skill.Code == "riposte")
        {
            var effect = new RiposteFormationEffectView3D();
            target.AddChild(effect);
            return true;
        }
        if (skill.Code == "turtle_formation")
        {
            var effect = new TurtleFormationEffectView3D();
            target.AddChild(effect);
            return true;
        }
        if (skill.Code == "evasion")
        {
            target.AddChild(new EvasionWindEffectView3D());
            return true;
        }
        if (skill.Code == "hold_the_line")
        {
            target.AddChild(new HoldTheLineArrowEffectView3D());
            return true;
        }
        if (skill.Code == "brace")
        {
            target.AddChild(new BraceArmorEffectView3D());
            return true;
        }
        if (skill.Code == "field_medic")
        {
            target.AddChild(new FieldMedicCrossEffectView3D());
            return true;
        }
        if (skill.Code == "regroup")
        {
            target.AddChild(new RegroupSyringeEffectView3D());
            return true;
        }
        if (skill.Code == "rally")
        {
            target.AddChild(new RallyWarDrumEffectView3D());
            return true;
        }
        if (skill.Code == "resupply")
        {
            target.AddChild(new ResupplyCrateEffectView3D());
            return true;
        }
        if (skill.Code == "second_wind")
        {
            target.AddChild(new SecondWindRebirthEffectView3D());
            return true;
        }
        if (skill.Code == "patch")
        {
            target.AddChild(new PatchCrossEffectView3D());
            return true;
        }
        if (skill.Code == "lightning")
        {
            target.AddChild(new LightningGlbEffectView3D());
            return true;
        }
        if (skill.Code == "crush")
        {
            var parent = target.GetParent();
            if (parent is null) return false;
            var effect = new CrushShieldBreakEffectView3D();
            parent.AddChild(effect);
            effect.GlobalPosition = target.GlobalPosition;
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
