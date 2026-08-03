using System.IO;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 진입점. Core로 시나리오를 로드해 맵 뷰에 넘기고 카메라를 맵 중앙에 둔다.
/// 노드는 Core를 호출하고 결과를 화면에 반영하는 역할만 한다.
/// </summary>
public partial class GameRoot : Node2D
{
    public override void _Ready()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

        var view = GetNode<HexMapView>("HexMapView");
        view.SetData(scenario.Map, scenario.Cities);

        var camera = GetNode<CameraController>("Camera2D");
        camera.Position = MapCenter(scenario.Map, view.HexSize);
        camera.MakeCurrent();
    }

    private static Vector2 MapCenter(HexMap map, float hexSize)
    {
        var min = HexLayout.ToPixel(new HexCoord(map.MinQ, map.MinR), hexSize);
        var max = HexLayout.ToPixel(new HexCoord(map.MaxQ, map.MaxR), hexSize);
        return (min + max) / 2f;
    }

    // 프로젝트 디렉토리에서 위로 올라가며 리포지토리의 data 디렉토리를 찾는다.
    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "factions.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data 디렉토리를 찾지 못했습니다.");
    }
}
