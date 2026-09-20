using Godot;

namespace SanguoSLG.Game;

/// <summary>맹호격 명중점에 대호가 나타나 편대 중앙을 덮치고 사라지는 1회성 연출.</summary>
public sealed partial class TigerStrikeEffectView3D : Node3D
{
    public bool HasTigerModel { get; private set; }
    public bool ReachedFormationCenter { get; private set; }
    public bool AttackCompleted { get; private set; }
    public float AttackTravelDistance { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.03f, 0f);
        var tiger = GD.Load<PackedScene>("res://assets/models/troop-great-tiger.glb").Instantiate<Node3D>();
        tiger.Name = "TigerStrike_GreatTiger";
        tiger.Position = new Vector3(-0.68f, 0f, 0f);
        tiger.Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f);
        tiger.Scale = Vector3.One * 0.01f;
        AddChild(tiger);
        MapView3D.TuneImportedMeshes(tiger);
        HasTigerModel = tiger.FindChild("body", true, false) is Node3D
            && tiger.FindChild("head", true, false) is Node3D;

        var head = tiger.FindChild("head", true, false) as Node3D;
        var body = tiger.FindChild("body", true, false) as Node3D;
        var destination = new Vector3(0.34f, 0.02f, 0f);
        AttackTravelDistance = tiger.Position.DistanceTo(destination);

        var attack = CreateTween();
        attack.TweenProperty(tiger, "scale", Vector3.One * 2.8f, 0.12f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        attack.TweenProperty(tiger, "position", destination, 0.34f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        attack.Parallel().TweenProperty(body, "rotation:x", -0.24f, 0.34f);
        if (head is not null)
            attack.Parallel().TweenProperty(head, "rotation:x", 0.48f, 0.34f);
        attack.TweenCallback(Callable.From(() => ReachedFormationCenter = true));
        attack.TweenInterval(0.18f);
        attack.TweenProperty(tiger, "position", new Vector3(0.62f, 0.06f, 0f), 0.18f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        attack.Parallel().TweenProperty(tiger, "scale", Vector3.One * 0.01f, 0.22f);
        attack.TweenCallback(Callable.From(() => AttackCompleted = true));

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.15 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
