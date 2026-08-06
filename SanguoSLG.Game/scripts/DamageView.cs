using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 파괴된 <b>형태</b>를 임의의 3D 모델에 입히는 공통 레이어.
/// 지형·건물마다 파괴본 모델을 따로 만들지 않고 세 층으로 처리한다:
/// (1) 재질 틴트 (2) 이름 기반 파츠 변형 (3) 잔해 프롭.
///
/// (2)는 Blender 스크립트가 공통 명명 규칙(_roof/_eave/_body/_post/fence_*/merlon*/chimney)을
/// 쓰기 때문에 성립한다 — 새 건물도 같은 이름을 쓰면 코드 수정 없이 적용된다.
///
/// 화염·연기 같은 연출은 형태와 직교하는 별개의 "효과"이므로 여기에 두지 않는다.
/// 효과 단계에서 별도 레이어로 정의한다(이전 화염 구현은 커밋 4fec587 참조).
/// </summary>
public static class DamageView
{
    private static PackedScene? _rubble;

    /// <summary>
    /// 모델에 파괴 표현을 입힌다. <paramref name="groundY"/>는 모델 로컬 기준 지면 높이
    /// (타일 일체형 모델 0.2, 성 기단 0.0864). <paramref name="seed"/>로 배치가 결정론적이다.
    /// </summary>
    public static void Apply(Node3D model, TileCondition condition, float groundY, ulong seed)
    {
        if (condition == TileCondition.Normal)
        {
            return;
        }

        TintTree(model, condition);

        // 루트 자신은 건드리지 않는다 — 루트를 기울이면 건물 전체가 기운다.
        foreach (var child in model.GetChildren())
        {
            TransformParts(child, condition, seed);
        }

        ScatterRubble(model, condition, groundY, seed);
    }

    // ── (1) 재질 틴트: 표면마다 원본 재질을 복제해 albedo만 바꾼다.
    // MaterialOverride를 쓰면 모델 전체가 한 색이 되므로 표면별 override를 쓴다.
    private static void TintTree(Node node, TileCondition condition)
    {
        if (node is MeshInstance3D instance && instance.Mesh is not null)
        {
            for (var surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                if (instance.GetActiveMaterial(surface) is not StandardMaterial3D source)
                {
                    continue;
                }

                var material = (StandardMaterial3D)source.Duplicate();
                material.AlbedoColor = Weather(material.AlbedoColor, condition);
                material.Roughness = Mathf.Min(1f, material.Roughness + 0.3f);
                material.Metallic = 0f;
                instance.SetSurfaceOverrideMaterial(surface, material);
            }
        }

        foreach (var child in node.GetChildren())
        {
            TintTree(child, condition);
        }
    }

    // 채도를 뺀 뒤 목표색으로 수렴시킨다. 단순히 어둡게 곱하면 원래 어두운 기와가
    // 새까매져 실루엣이 사라지므로, 먼지·재를 뒤집어쓴 회색으로 끌어당긴다.
    private static Color Weather(Color color, TileCondition condition)
    {
        var gray = color.R * 0.299f + color.G * 0.587f + color.B * 0.114f;
        var (target, amount) = condition == TileCondition.Destroyed
            ? (new Color(0.26f, 0.24f, 0.22f), 0.58f)
            : (new Color(0.44f, 0.42f, 0.38f), 0.45f);

        return new Color(
            Mathf.Lerp(Mathf.Lerp(color.R, gray, 0.55f), target.R, amount),
            Mathf.Lerp(Mathf.Lerp(color.G, gray, 0.55f), target.G, amount),
            Mathf.Lerp(Mathf.Lerp(color.B, gray, 0.55f), target.B, amount),
            color.A);
    }

    // ── (2) 이름 기반 파츠 변형
    private static void TransformParts(Node node, TileCondition condition, ulong seed)
    {
        if (node is Node3D part)
        {
            ApplyPartRule(part, part.Name.ToString(), condition, seed);
        }

        foreach (var child in node.GetChildren())
        {
            TransformParts(child, condition, seed);
        }
    }

    private static void ApplyPartRule(Node3D part, string name, TileCondition condition, ulong seed)
    {
        var wrecked = condition == TileCondition.Destroyed;
        var roll = Hash01(name, seed);

        // 성 여장(merlon) — 이가 빠진 것처럼 일부를 없앤다
        if (name.Contains("merlon"))
        {
            if (roll < (wrecked ? 0.55f : 0.35f))
            {
                part.Visible = false;
            }
            else
            {
                Lean(part, name, seed, wrecked ? 12f : 7f);
            }

            return;
        }

        // 마을·항구 외곽담 — 무너져 끊긴 담
        if (name.Contains("fence"))
        {
            if (roll < (wrecked ? 0.50f : 0.30f))
            {
                part.Visible = false;
            }
            else
            {
                Lean(part, name, seed, wrecked ? 16f : 9f);
            }

            return;
        }

        // 지붕·처마 — 내려앉고 기운다. 심하게 부서지면 일부는 아예 사라진다
        if (name.Contains("roof") || name.Contains("eave"))
        {
            if (wrecked && roll < 0.35f)
            {
                part.Visible = false;
                return;
            }

            Lean(part, name, seed, wrecked ? 18f : 10f);
            part.Position += new Vector3(0f, -0.010f - roll * 0.018f, 0f);
            return;
        }

        // 굴뚝·깃대·기둥처럼 가느다란 수직 부재 — 기울어진다
        if (name.Contains("chimney") || name.Contains("pole") || name.Contains("post"))
        {
            Lean(part, name, seed, wrecked ? 20f : 11f);
        }
    }

    // 기울이면 이웃 면과 새로 겹칠 수 있으므로 미세 오프셋으로 동일 평면을 깬다
    // (이번 작업에서 반복해서 겪은 z-파이팅 대비).
    private static void Lean(Node3D part, string name, ulong seed, float degrees)
    {
        var tiltX = (Hash01(name + "#x", seed) * 2f - 1f) * degrees;
        var tiltZ = (Hash01(name + "#z", seed) * 2f - 1f) * degrees;
        part.RotationDegrees += new Vector3(tiltX, 0f, tiltZ);
        // 해시가 0에 가까우면 오프셋이 사라지므로 최소 간격을 보장한다
        part.Position += new Vector3(0f, 0.002f + Hash01(name + "#y", seed) * 0.004f, 0f);
    }

    // ── (3) 잔해 프롭: 모델 1개를 회전·크기를 달리해 흩뿌린다
    private static void ScatterRubble(Node3D model, TileCondition condition, float groundY, ulong seed)
    {
        _rubble ??= GD.Load<PackedScene>("res://assets/models/rubble.glb");

        var count = condition == TileCondition.Destroyed ? 5 : 3;
        for (var i = 0; i < count; i++)
        {
            var piece = _rubble.Instantiate<Node3D>();
            var angle = Hash01($"rubble{i}#a", seed) * Mathf.Tau;
            var radius = 0.10f + Hash01($"rubble{i}#r", seed) * 0.30f;
            piece.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                // 잔해 바닥면은 타일 윗면과 평행하다 — 충분히 띄우지 않으면 깜빡인다.
                // 지금까지 효과가 있었던 간격(0.006~0.016)에 맞춰 잡는다.
                groundY + 0.012f,
                Mathf.Sin(angle) * radius);
            piece.RotationDegrees = new Vector3(0f, Hash01($"rubble{i}#yaw", seed) * 360f, 0f);
            piece.Scale = Vector3.One * (0.7f + Hash01($"rubble{i}#s", seed) * 0.7f);
            model.AddChild(piece);
        }
    }

    // 이름+시드로 0~1을 만드는 결정론적 해시(FNV-1a). 같은 맵이면 같은 파괴 모습이 나온다.
    private static float Hash01(string text, ulong seed)
    {
        var hash = 1469598103934665603UL ^ seed;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 1099511628211UL;
        }

        return (hash % 100000UL) / 100000f;
    }
}
