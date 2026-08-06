using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 마을 타일의 생활감 연출: 작은 주민 여러 명이 랜덤하게 나타나 담 안을 걸어다니다 사라진다.
/// 표현 전용(게임 규칙 없음). 타일 좌표 시드를 받아 같은 맵이면 같은 연출을 반복한다.
/// 건물·우물·호수는 원형 장애물로 등록해 목적지·경로 모두 피해 다닌다.
/// </summary>
public partial class VillagerAmbience : Node3D
{
    /// <summary>타일 좌표 기반 시드 — MapView3D가 배치 시 지정한다.</summary>
    public ulong Seed { get; set; }

    /// <summary>동시에 배회할 수 있는 최대 주민 수.</summary>
    public int MaxVillagers { get; set; } = 3;

    /// <summary>false면 새 주민이 나오지 않는다(있던 주민은 마저 퇴장).
    /// 게임 진행 상태(마을 불탐·황폐 등)와 연동하기 위한 런타임 스위치.</summary>
    public bool SpawnEnabled { get; set; } = true;

    /// <summary>피해 다닐 원형 장애물 목록 — (x, z, 반경), 타일 로컬 좌표.</summary>
    public Vector3[] Obstacles { get; set; } = System.Array.Empty<Vector3>();

    /// <summary>주민 발밑 높이 — 마을(타일 윗면) 0.2, 성(기단 윗면) 0.0864.</summary>
    public float GroundY { get; set; } = 0.2f;

    private const float WanderRadius = 0.32f; // 담(0.50) 안쪽 배회 반경
    private const float WalkSpeed = 0.10f;    // 초당 이동 거리
    private const float FadeTime = 0.5f;      // 나타남/사라짐 시간
    private const float BodyRadius = 0.022f;  // 장애물 판정에 더할 주민 반경

    private static readonly Color[] RobeColors =
    {
        new(0.26f, 0.32f, 0.55f), // 쪽빛
        new(0.48f, 0.30f, 0.16f), // 갈색
        new(0.30f, 0.46f, 0.24f), // 풀빛
        new(0.52f, 0.24f, 0.20f), // 팥죽색
        new(0.82f, 0.78f, 0.70f), // 무명(흰옷)
        new(0.22f, 0.22f, 0.28f), // 먹빛
    };

    private enum Phase { Waiting, Appearing, Walking, Disappearing }

    private sealed class Actor
    {
        public Node3D? Node;
        public Phase Phase = Phase.Waiting;
        public float Timer;
        public int SegmentsLeft;
        public Vector3 From;
        public Vector3 To;
        public float WalkTime;
        public float WalkDuration;
        public float BobClock;
    }

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Actor> _actors = new();
    private PackedScene _model = null!;

    public override void _Ready()
    {
        _model = GD.Load<PackedScene>("res://assets/models/villager.glb");
        _rng.Seed = Seed;
        for (var i = 0; i < MaxVillagers; i++)
        {
            // 초기 대기를 넓게 흩어 등장 타이밍이 겹치지 않게 한다
            _actors.Add(new Actor { Timer = _rng.RandfRange(1.5f, 9f) });
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        foreach (var actor in _actors)
        {
            Step(actor, dt);
        }
    }

    private void Step(Actor a, float dt)
    {
        switch (a.Phase)
        {
            case Phase.Waiting:
                a.Timer -= dt;
                if (a.Timer <= 0f)
                {
                    if (SpawnEnabled)
                    {
                        Spawn(a);
                    }
                    else
                    {
                        a.Timer = 1f; // 꺼져 있으면 잠시 후 다시 확인
                    }
                }

                break;

            case Phase.Appearing:
                a.Timer += dt;
                a.Node!.Scale = Vector3.One * Mathf.Clamp(a.Timer / FadeTime, 0.02f, 1f);
                if (a.Timer >= FadeTime)
                {
                    BeginNextSegment(a);
                }

                break;

            case Phase.Walking:
                a.WalkTime += dt;
                a.BobClock += dt;
                var t = Mathf.Clamp(a.WalkTime / a.WalkDuration, 0f, 1f);
                var pos = a.From.Lerp(a.To, t);
                pos.Y = GroundY + Mathf.Sin(a.BobClock * 9f) * 0.003f; // 종종걸음 들썩임
                a.Node!.Position = pos;
                if (t >= 1f)
                {
                    if (a.SegmentsLeft > 0)
                    {
                        BeginNextSegment(a);
                    }
                    else
                    {
                        a.Phase = Phase.Disappearing;
                        a.Timer = FadeTime;
                    }
                }

                break;

            case Phase.Disappearing:
                a.Timer -= dt;
                a.Node!.Scale = Vector3.One * Mathf.Max(a.Timer / FadeTime, 0.02f);
                if (a.Timer <= 0f)
                {
                    a.Node.QueueFree();
                    a.Node = null;
                    a.Phase = Phase.Waiting;
                    a.Timer = _rng.RandfRange(4f, 14f);
                }

                break;
        }
    }

    private void Spawn(Actor a)
    {
        a.Node = _model.Instantiate<Node3D>();
        a.Node.Position = SamplePoint();
        a.Node.Scale = Vector3.One * 0.02f;
        TintRobe(a.Node, RobeColors[_rng.RandiRange(0, RobeColors.Length - 1)]);
        MapView3D.DisableTinyShadowCasters(a.Node);
        AddChild(a.Node);

        a.SegmentsLeft = _rng.RandiRange(1, 3);
        a.Phase = Phase.Appearing;
        a.Timer = 0f;
        a.BobClock = 0f;
    }

    private void BeginNextSegment(Actor a)
    {
        a.SegmentsLeft--;
        a.From = a.Node!.Position with { Y = GroundY };

        // 장애물을 가로지르지 않는 목적지를 고른다(횟수 제한 후 포기하면 짧게 제자리걸음)
        a.To = a.From;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var candidate = SamplePoint();
            if (SegmentClear(a.From, candidate))
            {
                a.To = candidate;
                break;
            }
        }

        a.WalkDuration = Mathf.Max(a.From.DistanceTo(a.To) / WalkSpeed, 0.4f);
        a.WalkTime = 0f;
        a.Phase = Phase.Walking;

        var dir = a.To - a.From;
        if (dir.LengthSquared() > 1e-6f)
        {
            a.Node.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Z), 0f);
        }
    }

    /// <summary>담 안이면서 장애물 밖인 지점을 샘플링한다.</summary>
    private Vector3 SamplePoint()
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var angle = _rng.RandfRange(0f, Mathf.Tau);
            var radius = WanderRadius * Mathf.Sqrt(_rng.Randf()); // 원 안 균등 분포
            var p = new Vector3(Mathf.Cos(angle) * radius, GroundY, Mathf.Sin(angle) * radius);
            if (!InsideObstacle(p))
            {
                return p;
            }
        }

        return new Vector3(0f, GroundY, -0.40f); // 최후 대비: 남쪽 출입구 앞
    }

    private bool InsideObstacle(Vector3 p)
    {
        foreach (var o in Obstacles)
        {
            var r = o.Z + BodyRadius;
            var dx = p.X - o.X;
            var dz = p.Z - o.Y;
            if (dx * dx + dz * dz < r * r)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>선분 from→to가 모든 장애물 원을 비껴가는지 검사한다.</summary>
    private bool SegmentClear(Vector3 from, Vector3 to)
    {
        var a = new Vector2(from.X, from.Z);
        var b = new Vector2(to.X, to.Z);
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        foreach (var o in Obstacles)
        {
            var c = new Vector2(o.X, o.Y);
            var r = o.Z + BodyRadius;
            var t = lenSq < 1e-8f ? 0f : Mathf.Clamp((c - a).Dot(ab) / lenSq, 0f, 1f);
            if (a.Lerp(b, t).DistanceSquaredTo(c) < r * r)
            {
                return false;
            }
        }

        return true;
    }

    // 도포(body) 메시만 팔레트 색으로 덧입힌다 — 머리·삿갓은 원래 색 유지.
    private static void TintRobe(Node node, Color color)
    {
        if (node is MeshInstance3D mesh && node.Name.ToString().Contains("body"))
        {
            mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f };
            return;
        }

        foreach (var child in node.GetChildren())
        {
            TintRobe(child, color);
        }
    }
}
