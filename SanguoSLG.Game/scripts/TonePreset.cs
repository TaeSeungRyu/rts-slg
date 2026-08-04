using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 화면 색 톤 프리셋. --tone=이름 으로 선택한다(기본 pastel).
/// 사용자가 스크린샷 비교로 최종 톤을 고르기 위한 장치.
/// </summary>
public sealed record TonePreset(
    Color SkyTop,
    Color SkyHorizon,
    Color GroundBottom,
    float Ambient,
    float Exposure,
    float Brightness,
    float Contrast,
    float Saturation,
    float SunEnergy,
    Color SunColor,
    float Vignette,
    float FogDensity)
{
    public static readonly IReadOnlyDictionary<string, TonePreset> All = new Dictionary<string, TonePreset>
    {
        // 연한 파스텔(현재 기본): 밝기 유지 + 채도 다운
        ["pastel"] = new(
            new Color(0.62f, 0.70f, 0.80f), new Color(0.86f, 0.85f, 0.81f), new Color(0.42f, 0.47f, 0.54f),
            Ambient: 0.75f, Exposure: 1.0f, Brightness: 1.06f, Contrast: 0.94f, Saturation: 0.72f,
            SunEnergy: 1.15f, SunColor: new Color(1f, 0.96f, 0.88f), Vignette: 0.28f, FogDensity: 0.004f),

        // 원색 그대로: 색 보정 없음, 에셋 본연의 쨍한 색
        ["vivid"] = new(
            new Color(0.35f, 0.46f, 0.62f), new Color(0.78f, 0.75f, 0.68f), new Color(0.13f, 0.19f, 0.27f),
            Ambient: 0.7f, Exposure: 1.05f, Brightness: 1f, Contrast: 1f, Saturation: 1f,
            SunEnergy: 1.25f, SunColor: new Color(1f, 0.95f, 0.86f), Vignette: 0.2f, FogDensity: 0.004f),

        // 차분한 주간: 살짝만 눌러 자연스러운 낮
        ["muted"] = new(
            new Color(0.48f, 0.57f, 0.68f), new Color(0.80f, 0.78f, 0.72f), new Color(0.28f, 0.33f, 0.40f),
            Ambient: 0.7f, Exposure: 0.95f, Brightness: 1.0f, Contrast: 1.02f, Saturation: 0.85f,
            SunEnergy: 1.1f, SunColor: new Color(1f, 0.95f, 0.86f), Vignette: 0.35f, FogDensity: 0.004f),

        // 따뜻한 황혼빛: 노을 낀 오후
        ["warm"] = new(
            new Color(0.55f, 0.50f, 0.52f), new Color(0.90f, 0.78f, 0.62f), new Color(0.30f, 0.26f, 0.28f),
            Ambient: 0.7f, Exposure: 0.98f, Brightness: 1.02f, Contrast: 1.0f, Saturation: 0.9f,
            SunEnergy: 1.2f, SunColor: new Color(1f, 0.85f, 0.65f), Vignette: 0.35f, FogDensity: 0.005f),

        // 수묵담채: 채도를 크게 빼 담백한 동양화 느낌
        ["inkwash"] = new(
            new Color(0.72f, 0.75f, 0.78f), new Color(0.88f, 0.88f, 0.86f), new Color(0.50f, 0.52f, 0.55f),
            Ambient: 0.8f, Exposure: 1.0f, Brightness: 1.08f, Contrast: 0.92f, Saturation: 0.45f,
            SunEnergy: 1.05f, SunColor: new Color(0.98f, 0.97f, 0.95f), Vignette: 0.3f, FogDensity: 0.006f),
    };

    /// <summary>커맨드라인(--tone=이름)에서 프리셋을 고른다. 없거나 모르면 inkwash(사용자 선택 기본값).</summary>
    public static TonePreset FromCmdline()
    {
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg.StartsWith("--tone=") && All.TryGetValue(arg["--tone=".Length..], out var preset))
            {
                return preset;
            }
        }

        return All["inkwash"];
    }
}
