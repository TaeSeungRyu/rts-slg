using System;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 범용 효과 부착 진입점(doc/design-effect.md 1단계). 임의의 <see cref="Node3D"/>에 효과를
/// 자식으로 붙이고, 반환한 노드를 QueueFree하면 뗀다. 한 대상에 여러 효과를 겹쳐 붙일 수 있다.
/// 크기는 대상 크기에 맞춰 <paramref name="scale"/>로 받는다 — 타일 반경 0.5774짜리 작은
/// 월드라 엔진 기본값·미터 상수를 그대로 쓰면 안 된다.
/// 효과는 표현 전용이며 시뮬레이션에 영향을 주지 않는다(Core는 효과를 모른다).
/// </summary>
public static class EffectView
{
    // 효과별 적용 대상 범위(doc/design-effect.md "적용 대상 제약")의 코드 단일 출처.
    // 발동 규칙 계층과 검수 하네스 모두 여기를 참조한다.
    public static EffectTargetScope ScopeOf(EffectKind kind) => kind switch
    {
        EffectKind.Tear or EffectKind.Shatter or EffectKind.SoulRise => EffectTargetScope.Unit,
        EffectKind.Villagers => EffectTargetScope.Building,
        _ => EffectTargetScope.Both,
    };

    // loop=false는 1회성 효과(SoulRise·Lightning)에서만 의미가 있다 — 재생을 마치면
    // 반환된 루트를 효과가 스스로 QueueFree한다. 나머지 효과는 항상 반복 표시다.
    public static Node3D Attach(Node3D target, EffectKind kind, float scale = 1f, bool loop = true)
    {
        var root = new Node3D { Name = $"Effect_{kind}" };
        target.AddChild(root);

        switch (kind)
        {
            case EffectKind.Fire:
                BuildFire(root, scale);
                break;
            case EffectKind.Haze:
                BuildHaze(root, scale);
                break;
            case EffectKind.Flies:
                root.AddChild(new FliesEffect { S = scale });
                break;
            case EffectKind.Flood:
                root.AddChild(new FloodEffect { S = scale });
                break;
            case EffectKind.Skulls:
                root.AddChild(new SkullsEffect { S = scale });
                break;
            case EffectKind.Daze:
                root.AddChild(new DazeEffect { S = scale });
                break;
            case EffectKind.Bubbles:
                root.AddChild(new BubblesEffect { S = scale });
                break;
            case EffectKind.Burst:
                root.AddChild(new BurstEffect { S = scale });
                break;
            case EffectKind.Tear:
                root.AddChild(new TearEffect { S = scale, Target = target });
                break;
            case EffectKind.Shatter:
                root.AddChild(new ShatterEffect { S = scale, Target = target });
                break;
            case EffectKind.Confusion:
                root.AddChild(new ConfusionEffect { S = scale });
                break;
            case EffectKind.SoulRise:
                root.AddChild(new SoulRiseEffect { S = scale, Loop = loop });
                break;
            case EffectKind.Lightning:
                root.AddChild(new LightningEffect { S = scale, Loop = loop });
                break;
            default:
                throw new InvalidOperationException($"미구현 효과: {kind}");
        }

        return root;
    }

    // 빨강색 불이 피어오르는 효과: 화염 파티클 + 그을음 연기 + 불빛(프로토타입 커밋 4fec587
    // 값을 출발점으로, 색을 빨강 쪽으로 당김). scale로 대상 크기에 맞춘다.
    private static void BuildFire(Node3D root, float s)
    {
        root.AddChild(BuildFlames(s));
        root.AddChild(BuildSootSmoke(s));
        root.AddChild(BuildGlow(s));
    }

    private static CpuParticles3D BuildFlames(float s)
    {
        // 빨강 불 — 뜨거운 심지(밝은 주황)에서 붉은 몸통으로. 심지가 없으면 불로 안 읽힌다.
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1.0f, 0.52f, 0.16f, 0.80f));
        gradient.AddPoint(0.45f, new Color(0.92f, 0.18f, 0.06f, 0.62f));
        gradient.SetColor(1, new Color(0.48f, 0.05f, 0.02f, 0f));

        return new CpuParticles3D
        {
            // 가산합성이라 겹치면 흰색까지 포화된다 — 수를 낮게 유지한다
            Position = new Vector3(0f, 0.04f * s, 0f),
            Amount = 18,
            Lifetime = 0.7f,
            Preprocess = 1.5f,
            Mesh = PuffMesh(0.030f * s, BaseMaterial3D.BlendModeEnum.Add),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.20f * s, 0.02f, 0.20f * s),
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 12f,
            InitialVelocityMin = 0.20f * s,
            InitialVelocityMax = 0.40f * s,
            Gravity = new Vector3(0f, 0.14f * s, 0f),
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.2f,
            ColorRamp = gradient,
        };
    }

    private static CpuParticles3D BuildSootSmoke(float s)
    {
        // 그을음은 하늘색보다 확실히 어두워야 연기로 읽힌다
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.12f, 0.11f, 0.10f, 0.85f));
        gradient.AddPoint(0.5f, new Color(0.22f, 0.21f, 0.20f, 0.55f));
        gradient.SetColor(1, new Color(0.36f, 0.35f, 0.34f, 0f));

        return new CpuParticles3D
        {
            Position = new Vector3(0f, 0.28f * s, 0f),
            Amount = 22,
            Lifetime = 3.4f,
            Preprocess = 4f,
            Mesh = PuffMesh(0.075f * s, BaseMaterial3D.BlendModeEnum.Mix),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.14f * s, 0.02f, 0.14f * s),
            Direction = new Vector3(0.25f, 1f, 0f),
            Spread = 14f,
            InitialVelocityMin = 0.18f * s,
            InitialVelocityMax = 0.32f * s,
            Gravity = new Vector3(0.10f * s, 0.10f * s, 0.03f * s),
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 2.2f,
            ColorRamp = gradient,
        };
    }

    // 불은 빛이 있어야 3D에서 읽힌다. 그림자는 끈다(비용 + 얇은 부재 어른거림 방지).
    private static OmniLight3D BuildGlow(float s) => new()
    {
        Position = new Vector3(0f, 0.18f * s, 0f),
        LightColor = new Color(1.0f, 0.45f, 0.18f),
        LightEnergy = 1.6f,
        OmniRange = 1.4f * s,
        ShadowEnabled = false,
    };

    // 회색 안개(연무): 대상 주위에 낮고 넓게 깔리는 반투명 회색 뭉치. 연기(Smoke)와 달리
    // 위로 솟지 않고 제자리에서 끼었다 걷힌다 — 속도를 거의 0으로, 알파를 낮게 유지한다.
    private static void BuildHaze(Node3D root, float s)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.12f, 0.12f, 0.15f, 0f));
        gradient.AddPoint(0.3f, new Color(0.09f, 0.09f, 0.12f, 0.62f));
        gradient.SetColor(1, new Color(0.07f, 0.07f, 0.10f, 0f));

        root.AddChild(new CpuParticles3D
        {
            Position = new Vector3(0f, 0.12f * s, 0f),
            Amount = 9,
            Lifetime = 3.6f,
            Preprocess = 3f,
            // 랜덤은 공짜다 — 개수·에미터가 아니라 입자당 편차만 키운다. 방출 타이밍·수명·
            // 속도를 흩어 규칙적으로 뭉치지 않게 하고, 크기 편차를 넓혀 뭉게뭉게 불규칙하게.
            Randomness = 0.9f,
            LifetimeRandomness = 0.6f,
            Mesh = PuffMesh(0.10f * s, BaseMaterial3D.BlendModeEnum.Mix),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.26f * s, 0.10f * s, 0.26f * s),
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 45f,
            InitialVelocityMin = 0.01f * s,
            InitialVelocityMax = 0.08f * s,
            Gravity = Vector3.Zero,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 3.2f,
            ColorRamp = gradient,
        });
    }

    private static SphereMesh PuffMesh(float radius, BaseMaterial3D.BlendModeEnum blend) => new()
    {
        Radius = radius,
        Height = radius * 2f,
        RadialSegments = 6,
        Rings = 3,
        Material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = blend,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };
}
