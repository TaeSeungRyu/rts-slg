using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>공격자 쪽에서 작은 대호 무리가 나타나 적 편대를 같은 방향으로 덮치는 1회성 연출.</summary>
public sealed partial class TigerStrikeEffectView3D : Node3D
{
    private Vector3 _attackDirection = Vector3.Forward;
    private readonly List<Node3D> _tigers = [];

    public int TigerCount => _tigers.Count;
    public int ReachedFormationCenterCount { get; private set; }
    public int CompletedTigerCount { get; private set; }
    public Vector3 AttackDirection => _attackDirection;
    public float AttackTravelDistance { get; private set; }

    public void Configure(Vector3 casterGlobalPosition, Vector3 targetGlobalPosition)
    {
        _attackDirection = targetGlobalPosition - casterGlobalPosition;
        _attackDirection.Y = 0f;
        _attackDirection = _attackDirection.LengthSquared() > 0.000001f
            ? _attackDirection.Normalized()
            : Vector3.Forward;
    }

    public override void _Ready()
    {
        var side = Vector3.Up.Cross(_attackDirection).Normalized();
        var startCentre = -_attackDirection * 0.72f;
        var destinationCentre = _attackDirection * 0.42f;
        AttackTravelDistance = startCentre.DistanceTo(destinationCentre);

        for (var i = 0; i < 4; i++)
        {
            var lane = i - 1.5f;
            var tiger = GD.Load<PackedScene>("res://assets/models/troop-great-tiger.glb").Instantiate<Node3D>();
            tiger.Name = $"TigerStrike_GreatTiger_{i + 1}";
            tiger.Position = startCentre + side * lane * 0.13f - _attackDirection * ((i % 2) * 0.08f);
            tiger.Rotation = new Vector3(0f, Mathf.Atan2(_attackDirection.X, _attackDirection.Z), 0f);
            tiger.Scale = Vector3.One * 0.01f;
            AddChild(tiger);
            MapView3D.TuneImportedMeshes(tiger);
            _tigers.Add(tiger);

            var body = tiger.FindChild("body", true, false) as Node3D;
            var head = tiger.FindChild("head", true, false) as Node3D;
            var destination = destinationCentre + side * lane * 0.11f;
            var delay = i * 0.07f;
            var attack = CreateTween();
            attack.TweenInterval(delay);
            attack.TweenProperty(tiger, "scale", Vector3.One * 1.35f, 0.10f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            attack.TweenProperty(tiger, "position", destination, 0.38f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            if (body is not null)
                attack.Parallel().TweenProperty(body, "rotation:x", -0.22f, 0.38f);
            if (head is not null)
                attack.Parallel().TweenProperty(head, "rotation:x", 0.46f, 0.38f);
            attack.TweenCallback(Callable.From(() => ReachedFormationCenterCount++));
            attack.TweenInterval(0.10f);
            attack.TweenProperty(tiger, "position", destination + _attackDirection * 0.24f, 0.14f);
            attack.Parallel().TweenProperty(tiger, "scale", Vector3.One * 0.01f, 0.18f);
            attack.TweenCallback(Callable.From(() => CompletedTigerCount++));
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.35 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
