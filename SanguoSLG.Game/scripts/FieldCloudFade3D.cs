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
            .OfType<MeshInstance3D>().OrderBy(x => x.Name.ToString(), System.StringComparer.Ordinal).ToArray();
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
        var progress = _player.CurrentAnimationPosition / length;
        var visible = GetParent() is Node3D model && model.IsVisibleInTree();
        for (var i = 0; i < _clouds.Length; i++)
        {
            var cloud = _clouds[i];
            if (!GodotObject.IsInstanceValid(cloud)) continue;
            var phase = progress * _clouds.Length - i;
            var duration = System.Math.Min(1.6, _clouds.Length - i);
            cloud.Transparency = 1f - (visible && phase >= 0 && phase < duration ? Opacity(phase / duration) : 0f);
        }
    }
}
