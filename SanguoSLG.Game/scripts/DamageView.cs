using System;
using System.Collections.Generic;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 부서진 <b>형태</b>를 임의의 3D 모델에 입히는 공통 레이어.
/// 지형·건물마다 파괴본 모델을 따로 만들지 않고, 모델의 파츠 이름으로 찾아 변형한다.
///
/// <b>색은 입히지 않는다</b> — 색 변화는 나중에 조건에 따라 별도로 정의한다.
/// 화염·연기·잔해 같은 연출도 여기에 없다(효과 단계에서 정의, 1차 구현은 커밋 4fec587).
///
/// 사용자 정의 기준(2026-08-06):
/// - 성: 건물 1개 제거 후 그 자리에 네모 형태만 남김, 지붕 삐뚤어짐
/// - 마을: 건물 절반을 네모 형태만 남김, 모든 지붕 삐뚤어짐
/// - 항구: 모든 지붕 삐뚤어짐, 잔교에 구멍
/// - 논: 모가 띄엄띄엄 빠짐 / 공방: 지붕 삐뚤어짐 + 연기 멈춤 / 밭: 4조각 모델로 교체
/// </summary>
public static class DamageView
{
    /// <summary>부서짐 규칙이 다른 대상 종류.</summary>
    public enum Kind
    {
        /// <summary>지붕만 삐뚤어지는 일반 건물(공방 등).</summary>
        Plain,
        Castle,
        Village,
        Port,
        Paddy,
    }

    private const float CrookedDegrees = 13f;

    /// <summary>건물을 걷어낸 자리에 남기는 네모(터)의 높이 비율.</summary>
    private const float StubHeight = 0.4f;

    public static void Apply(Node3D model, TileCondition condition, Kind kind, ulong seed)
    {
        if (condition == TileCondition.Normal)
        {
            return;
        }

        var parts = new List<Node3D>();
        Collect(model, parts);

        MakeRoofsCrooked(parts, seed);

        switch (kind)
        {
            case Kind.Castle:
                StripBuildings(parts, CastleGroups(parts), 1, seed);
                break;

            case Kind.Village:
                var groups = BodyGroups(parts);
                StripBuildings(parts, groups, (groups.Count + 1) / 2, seed);
                break;

            case Kind.Port:
                HideSome(parts, "plank", 0.34f, seed);
                break;

            case Kind.Paddy:
                HideSome(parts, "rice", 0.45f, seed);
                break;
        }
    }

    // 루트 자신은 제외한다 — 루트를 기울이면 건물이 아니라 타일 전체가 기운다.
    private static void Collect(Node node, List<Node3D> into)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D part)
            {
                into.Add(part);
            }

            Collect(child, into);
        }
    }

    // ── 지붕 삐뚤어지게. 지붕과 그 처마는 한 덩어리이므로 같은 각도로 기울여야 서로 벌어지지 않는다.
    private static void MakeRoofsCrooked(List<Node3D> parts, ulong seed)
    {
        foreach (var part in parts)
        {
            var key = RoofGroupKey(part.Name.ToString());
            if (key is null)
            {
                continue;
            }

            var tiltX = (Hash01(key + "#x", seed) * 2f - 1f) * CrookedDegrees;
            var tiltZ = (Hash01(key + "#z", seed) * 2f - 1f) * CrookedDegrees;
            part.RotationDegrees += new Vector3(tiltX, 0f, tiltZ);
            // 기울이면 아래 부재와 새로 겹칠 수 있다 — 최소 간격을 둬 z-파이팅을 막는다.
            part.Position += new Vector3(0f, 0.003f, 0f);
        }
    }

    /// <summary>지붕·처마 파츠면 소속 건물 키를, 아니면 null을 돌려준다.</summary>
    private static string? RoofGroupKey(string name)
    {
        foreach (var suffix in new[] { "_topeave", "_roof", "_eave" })
        {
            var index = name.IndexOf(suffix, StringComparison.Ordinal);
            if (index > 0)
            {
                return name[..index];
            }
        }

        return null;
    }

    // ── 건물을 네모(터)만 남기고 걷어낸다.
    // 성 건물은 b0_t0 / b0_eave0 / b0_roof …, 마을·항구 건물은 tag_body / tag_roof … 로
    // 접두사가 같으므로 접두사 단위로 묶어 처리한다.
    private static void StripBuildings(List<Node3D> parts, List<string> groups, int count, ulong seed)
    {
        if (groups.Count == 0 || count <= 0)
        {
            return;
        }

        // 해시 순으로 정렬해 결정론적으로 고른다 — 같은 맵이면 같은 건물이 무너진다.
        groups.Sort((a, b) => Hash01(a + "#pick", seed).CompareTo(Hash01(b + "#pick", seed)));

        for (var i = 0; i < count && i < groups.Count; i++)
        {
            var group = groups[i];
            foreach (var part in parts)
            {
                var name = part.Name.ToString();
                if (!name.StartsWith(group + "_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsStubBody(name, group))
                {
                    Flatten(part, StubHeight);
                }
                else
                {
                    part.Visible = false;
                }
            }
        }
    }

    // 남길 네모: 마을·항구는 몸체(_body / _body1), 성은 1층(_t0).
    private static bool IsStubBody(string name, string group) =>
        name == group + "_body" || name == group + "_body1" || name == group + "_t0";

    /// <summary>파츠를 납작하게 눌러 터만 남긴다. 바닥면 높이는 그대로 유지한다.</summary>
    private static void Flatten(Node3D part, float factor)
    {
        if (part is not MeshInstance3D mesh || mesh.Mesh is null)
        {
            return;
        }

        // 로컬 AABB로 계산하면 Blender가 스케일을 노드에 뒀든 메시에 구웠든 똑같이 동작한다.
        var bottomLocal = mesh.Mesh.GetAabb().Position.Y;
        var bottom = part.Position.Y + part.Scale.Y * bottomLocal;
        var scaledY = part.Scale.Y * factor;

        part.Scale = part.Scale with { Y = scaledY };
        part.Position = part.Position with { Y = bottom - scaledY * bottomLocal };
    }

    // ── 이름에 keyword가 든 파츠를 비율만큼 숨긴다(잔교 구멍, 논에서 빠진 모).
    private static void HideSome(List<Node3D> parts, string keyword, float ratio, ulong seed)
    {
        foreach (var part in parts)
        {
            var name = part.Name.ToString();
            if (name.Contains(keyword) && Hash01(name + "#hide", seed) < ratio)
            {
                part.Visible = false;
            }
        }
    }

    // 성 건물 묶기: b0_t0, b0_p0_1_1, b0_roof … → "b0"
    private static List<string> CastleGroups(List<Node3D> parts)
    {
        var groups = new List<string>();
        foreach (var part in parts)
        {
            var name = part.Name.ToString();
            var underscore = name.IndexOf('_');
            if (underscore <= 1 || name[0] != 'b' || !char.IsDigit(name[1]))
            {
                continue;
            }

            var key = name[..underscore];
            if (!groups.Contains(key))
            {
                groups.Add(key);
            }
        }

        return groups;
    }

    // 마을·항구 건물 묶기: tag_body(또는 2단집의 tag_body1)를 가진 것이 건물 한 채
    private static List<string> BodyGroups(List<Node3D> parts)
    {
        var groups = new List<string>();
        foreach (var part in parts)
        {
            var name = part.Name.ToString();
            var key = name.EndsWith("_body1", StringComparison.Ordinal) ? name[..^6]
                : name.EndsWith("_body", StringComparison.Ordinal) ? name[..^5]
                : null;

            if (key is not null && !groups.Contains(key))
            {
                groups.Add(key);
            }
        }

        return groups;
    }

    // 이름+시드로 0~1을 만드는 결정론적 해시(FNV-1a). 같은 맵이면 같은 모습이 나온다.
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
