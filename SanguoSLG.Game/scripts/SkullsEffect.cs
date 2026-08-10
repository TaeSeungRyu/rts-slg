using System;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 여러 랜덤 크기의 해골이 아래에서 위로 나타났다 사라지는 효과(design-effect.md #5).
/// 파티클로 하면 해골이 제멋대로 돌아가 얼굴이 안 보여 회색 덩어리처럼 읽힌다 —
/// 노드로 만들어 매 프레임 카메라를 바라보게(빌보드) 해 눈·입이 늘 보이게 한다.
/// 각 해골은 서로 다른 위상의 주기로 떠오르며 크기가 0→최대→0으로 나타났다 사라진다.
/// 위상·자리는 인덱스에서 뽑아 난수를 쓰지 않는다.
/// </summary>
public partial class SkullsEffect : Node3D
{
    public float S = 1f;

    private const int Count = 5;
    private const float Period = 2.6f;

    private static Mesh? _mesh;

    private readonly MeshInstance3D[] _skulls = new MeshInstance3D[Count];
    private float _t;

    public override void _Ready()
    {
        for (var i = 0; i < Count; i++)
        {
            var skull = new MeshInstance3D { Mesh = SkullMesh(), Visible = false };
            AddChild(skull);
            _skulls[i] = skull;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var cam = GetViewport().GetCamera3D();

        for (var i = 0; i < Count; i++)
        {
            // 주기 안 위상(0..1) — 해골마다 어긋내 어떤 건 떠오르고 어떤 건 사라진다
            var cycle = Mathf.PosMod(_t / Period + i / (float)Count, 1f);
            var envelope = Mathf.Sin(Mathf.Pi * cycle); // 0 → 1 → 0
            var size = envelope * (1.6f + (i % 3) * 0.5f) * S;

            var skull = _skulls[i];
            if (size < 0.02f)
            {
                skull.Visible = false;
                continue;
            }

            var laneX = (i - (Count - 1) * 0.5f) * 0.06f * S;
            var laneZ = ((i * 7) % 5 - 2) * 0.04f * S;
            skull.Visible = true;
            skull.Position = new Vector3(laneX, 0.03f * S + cycle * 0.5f * S, laneZ);
            skull.Scale = new Vector3(size, size, size);

            // 카메라를 바라보게 — 모델 정면(+Z)이 카메라를 향하도록 -Z를 반대편에 두고 180도
            if (cam is not null)
            {
                skull.LookAt(2f * skull.GlobalPosition - cam.GlobalPosition, Vector3.Up);
            }
        }
    }

    private static Mesh SkullMesh()
    {
        if (_mesh is not null)
        {
            return _mesh;
        }

        var instance = GD.Load<PackedScene>("res://assets/models/prop-skull.glb").Instantiate<Node3D>();
        _mesh = FindMesh(instance) ?? throw new InvalidOperationException("prop-skull.glb에 메시가 없다");
        instance.QueueFree();
        return _mesh;
    }

    private static Mesh? FindMesh(Node node)
    {
        if (node is MeshInstance3D { Mesh: { } mesh })
        {
            return mesh;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindMesh(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
