using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 발사체 연출(표현 전용). 명중·피해 판정은 Core의 영역이고 여기는 날아가는 그림만 그린다.
/// 변형은 Basic뿐이며 Fire(불화살·불덩이)는 효과 단계에서 이 enum에 값을 더해 구현한다
/// (doc/design-effect.md — 발광 재질 + 꼬리 파티클을 변형별로 붙이면 된다).
/// </summary>
public static class ProjectileView
{
    public enum Variant
    {
        Basic,
    }

    /// <summary>화살 하나를 포물선으로 날린다. worldParent는 유닛과 함께 움직이지 않는 노드여야 한다.</summary>
    public static void SpawnArrow(Node3D worldParent, Vector3 from, Vector3 to, float seconds,
        Variant variant = Variant.Basic)
    {
        var arrow = BuildArrow(variant);
        worldParent.AddChild(arrow);
        arrow.GlobalPosition = from;

        var arc = (to - from).Length() * 0.16f;

        var tween = arrow.CreateTween();
        tween.TweenMethod(Callable.From((float t) =>
        {
            var pos = Sample(from, to, arc, t);
            var ahead = Sample(from, to, arc, Mathf.Min(t + 0.04f, 1f));
            arrow.GlobalPosition = pos;
            if ((ahead - pos).LengthSquared() > 0.0000001f)
            {
                arrow.LookAt(ahead);
            }
        }), 0f, 1f, seconds);
        tween.TweenCallback(Callable.From(arrow.QueueFree));
    }

    /// <summary>돌덩이를 높은 포물선으로 날린다(투석기).</summary>
    public static void SpawnStone(Node3D worldParent, Vector3 from, Vector3 to, float seconds,
        Variant variant = Variant.Basic)
    {
        var stone = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.022f, Height = 0.04f, RadialSegments = 7, Rings = 4 },
            MaterialOverride = BodyMaterial(variant, new Color(0.52f, 0.52f, 0.50f)),
        };
        worldParent.AddChild(stone);
        stone.GlobalPosition = from;

        var arc = (to - from).Length() * 0.24f;
        var tween = stone.CreateTween();
        tween.TweenMethod(Callable.From((float t) =>
        {
            stone.GlobalPosition = Sample(from, to, arc, t);
        }), 0f, 1f, seconds);
        tween.TweenCallback(Callable.From(stone.QueueFree));
    }

    // 변형별 몸체 재질. Fire가 생기면 여기서 발광을 켜고, 스폰 쪽에서 꼬리 파티클을 붙인다.
    private static StandardMaterial3D BodyMaterial(Variant variant, Color basic) =>
        variant switch
        {
            _ => new StandardMaterial3D { AlbedoColor = basic },
        };

    private static Vector3 Sample(Vector3 from, Vector3 to, float arc, float t) =>
        from.Lerp(to, t) + Vector3.Up * (arc * 4f * t * (1f - t));

    private static Node3D BuildArrow(Variant variant)
    {
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.008f, 0.008f, 0.11f) },
            // 두께 0.05 미만은 그림자맵 텍셀보다 얇아 acne로 깜빡인다 — 캐스팅 금지
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = BodyMaterial(variant, new Color(0.38f, 0.26f, 0.15f)),
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.013f, 0.013f, 0.022f) },
            Position = new Vector3(0f, 0f, -0.06f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.66f, 0.67f, 0.70f),
                Metallic = 0.6f,
                Roughness = 0.35f,
            },
        });
        return root;
    }
}
