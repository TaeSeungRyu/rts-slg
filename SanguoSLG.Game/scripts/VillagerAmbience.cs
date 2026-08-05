using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 마을 타일의 생활감 연출: 작은 주민이 랜덤하게 나타나 담 안을 걸어다니다 사라진다.
/// 표현 전용(게임 규칙 없음). 타일 좌표 시드를 받아 같은 맵이면 같은 연출을 반복한다.
/// </summary>
public partial class VillagerAmbience : Node3D
{
    /// <summary>타일 좌표 기반 시드 — MapView3D가 배치 시 지정한다.</summary>
    public ulong Seed { get; set; }

    private const float GroundY = 0.2f;       // 타일 윗면 높이
    private const float WanderRadius = 0.30f; // 담(0.50) 안쪽 배회 반경
    private const float WalkSpeed = 0.10f;    // 초당 이동 거리
    private const float FadeTime = 0.5f;      // 나타남/사라짐 시간

    private static readonly Color[] RobeColors =
    {
        new(0.26f, 0.32f, 0.55f), // 쪽빛
        new(0.48f, 0.30f, 0.16f), // 갈색
        new(0.30f, 0.46f, 0.24f), // 풀빛
        new(0.52f, 0.24f, 0.20f), // 팥죽색
    };

    private enum Phase { Waiting, Appearing, Walking, Disappearing }

    private readonly RandomNumberGenerator _rng = new();
    private PackedScene _model = null!;
    private Node3D? _villager;
    private Phase _phase = Phase.Waiting;
    private float _timer;          // Waiting/Appearing/Disappearing 진행 시계
    private int _segmentsLeft;     // 남은 배회 구간 수
    private Vector3 _from;
    private Vector3 _to;
    private float _walkTime;
    private float _walkDuration;
    private float _bobClock;

    public override void _Ready()
    {
        _model = GD.Load<PackedScene>("res://assets/models/villager.glb");
        _rng.Seed = Seed;
        _timer = _rng.RandfRange(1.5f, 6f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        switch (_phase)
        {
            case Phase.Waiting:
                _timer -= dt;
                if (_timer <= 0f)
                {
                    SpawnVillager();
                }

                break;

            case Phase.Appearing:
                _timer += dt;
                _villager!.Scale = Vector3.One * Mathf.Clamp(_timer / FadeTime, 0.02f, 1f);
                if (_timer >= FadeTime)
                {
                    BeginNextSegment();
                }

                break;

            case Phase.Walking:
                _walkTime += dt;
                _bobClock += dt;
                var t = Mathf.Clamp(_walkTime / _walkDuration, 0f, 1f);
                var pos = _from.Lerp(_to, t);
                pos.Y = GroundY + Mathf.Sin(_bobClock * 9f) * 0.003f; // 종종걸음 들썩임
                _villager!.Position = pos;
                if (t >= 1f)
                {
                    if (_segmentsLeft > 0)
                    {
                        BeginNextSegment();
                    }
                    else
                    {
                        _phase = Phase.Disappearing;
                        _timer = FadeTime;
                    }
                }

                break;

            case Phase.Disappearing:
                _timer -= dt;
                _villager!.Scale = Vector3.One * Mathf.Max(_timer / FadeTime, 0.02f);
                if (_timer <= 0f)
                {
                    _villager.QueueFree();
                    _villager = null;
                    _phase = Phase.Waiting;
                    _timer = _rng.RandfRange(4f, 12f);
                }

                break;
        }
    }

    private void SpawnVillager()
    {
        _villager = _model.Instantiate<Node3D>();
        _villager.Position = RandomPointInYard();
        _villager.Scale = Vector3.One * 0.02f;
        TintRobe(_villager, RobeColors[_rng.RandiRange(0, RobeColors.Length - 1)]);
        AddChild(_villager);

        _segmentsLeft = _rng.RandiRange(1, 3);
        _phase = Phase.Appearing;
        _timer = 0f;
        _bobClock = 0f;
    }

    private void BeginNextSegment()
    {
        _segmentsLeft--;
        _from = _villager!.Position with { Y = GroundY };
        _to = RandomPointInYard();
        _walkDuration = Mathf.Max(_from.DistanceTo(_to) / WalkSpeed, 0.3f);
        _walkTime = 0f;
        _phase = Phase.Walking;

        var dir = _to - _from;
        if (dir.LengthSquared() > 1e-6f)
        {
            _villager.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Z), 0f);
        }
    }

    private Vector3 RandomPointInYard()
    {
        var angle = _rng.RandfRange(0f, Mathf.Tau);
        var radius = WanderRadius * Mathf.Sqrt(_rng.Randf()); // 원 안 균등 분포
        return new Vector3(Mathf.Cos(angle) * radius, GroundY, Mathf.Sin(angle) * radius);
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
