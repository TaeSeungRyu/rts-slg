using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>분쇄 명중점에 네모 방패가 나타난 뒤 아홉 조각으로 깨져 흩어지는 효과.</summary>
public sealed partial class CrushShieldBreakEffectView3D : Node3D
{
    private readonly List<MeshInstance3D> _fragments = [];

    public int FragmentCount => _fragments.Count;
    public bool ShieldAppeared { get; private set; }
    public bool ShatterCompleted { get; private set; }
    public float MaxScatterDistance { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.38f, 0f);
        var steel = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.42f, 0.58f), Metallic = 0.82f, Roughness = 0.24f,
            EmissionEnabled = true, Emission = new Color(0.05f, 0.12f, 0.20f), EmissionEnergyMultiplier = 1.4f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var edge = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.79f, 0.84f), Metallic = 0.95f, Roughness = 0.14f,
            EmissionEnabled = true, Emission = new Color(0.20f, 0.30f, 0.38f), EmissionEnergyMultiplier = 1.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        for (var row = -1; row <= 1; row++)
        for (var col = -1; col <= 1; col++)
        {
            var fragment = new MeshInstance3D
            {
                Name = $"CrushShield_Fragment_{row + 2}_{col + 2}",
                Mesh = new BoxMesh { Size = new Vector3(0.145f, 0.145f, 0.032f), Material = row == 0 && col == 0 ? edge : steel },
                Position = new Vector3(col * 0.148f, row * 0.148f, 0f),
                Scale = Vector3.One * 0.01f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(fragment);
            _fragments.Add(fragment);

            var delay = (row + col + 2) * 0.018f;
            var rest = fragment.Position;
            var direction = new Vector3(col * 0.75f, row * 0.75f + 0.18f, 0.45f).Normalized();
            var distance = 0.30f + (row * row + col * col) * 0.07f;
            MaxScatterDistance = Mathf.Max(MaxScatterDistance, distance);
            var tween = CreateTween();
            tween.TweenInterval(delay);
            tween.TweenProperty(fragment, "scale", Vector3.One, 0.16f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.22f);
            tween.TweenProperty(fragment, "position", rest + direction * distance, 0.38f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(fragment, "rotation", new Vector3(row * 1.4f, col * 1.2f, (row - col) * 1.5f), 0.38f);
            tween.Parallel().TweenProperty(fragment, "scale", Vector3.One * 0.05f, 0.42f);
        }

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.24 };
        AddChild(appeared);
        appeared.Timeout += () => ShieldAppeared = true;
        appeared.Start();
        var shattered = new Godot.Timer { OneShot = true, WaitTime = 0.92 };
        AddChild(shattered);
        shattered.Timeout += () => ShatterCompleted = true;
        shattered.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.22 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
