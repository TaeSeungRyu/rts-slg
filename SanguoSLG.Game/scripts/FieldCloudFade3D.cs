using Godot;
using System.Linq;

namespace SanguoSLG.Game;

public partial class FieldCloudFade3D : Node
{
    private MeshInstance3D[] _clouds = [];
    private AnimationPlayer? _player;

    public override void _Ready()
    {
        var model = GetParent();
        _clouds = model.FindChildren("formation_cloud_*", "MeshInstance3D", true, false)
            .OfType<MeshInstance3D>().ToArray();
        _player = model.FindChildren("*", "AnimationPlayer", true, false)
            .OfType<AnimationPlayer>().FirstOrDefault();
        foreach (var cloud in _clouds) cloud.Transparency = 1f;
    }

    public static float Opacity(double progress)
    {
        var p = (float)progress;
        return Mathf.SmoothStep(0f, 0.20f, p) * (1f - Mathf.SmoothStep(0.78f, 1f, p)) * 0.72f;
    }

    public override void _Process(double delta)
    {
        if (_player is null || !GodotObject.IsInstanceValid(_player) || !_player.IsPlaying()) return;
        var length = _player.CurrentAnimationLength;
        if (length <= 0) return;
        var transparency = 1f - Opacity(_player.CurrentAnimationPosition / length);
        foreach (var cloud in _clouds)
            if (GodotObject.IsInstanceValid(cloud)) cloud.Transparency = transparency;
    }
}
