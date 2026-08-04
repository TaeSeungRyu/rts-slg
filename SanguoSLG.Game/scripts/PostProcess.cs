using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 화면 포스트프로세싱. 아트 없이 글로우·색보정·톤맵으로 "화면 질감"을 올린다.
/// HDR 2D(project.godot)와 함께 동작한다.
/// </summary>
public partial class PostProcess : WorldEnvironment
{
    public override void _Ready()
    {
        Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Canvas,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,

            GlowEnabled = true,
            GlowIntensity = 0.6f,
            GlowStrength = 1.0f,
            GlowBloom = 0.12f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            GlowHdrThreshold = 0.85f,

            AdjustmentEnabled = true,
            AdjustmentBrightness = 1.02f,
            AdjustmentContrast = 1.08f,
            AdjustmentSaturation = 1.06f,
        };
    }
}
