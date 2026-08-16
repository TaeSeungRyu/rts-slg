using System.IO;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
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

        // 유닛 1기를 첫 도시에 스폰(슬라이스용). units.json은 이후.
        var startCity = scenario.Cities[0];
        var unit = new Unit(new UnitId(1), startCity.Owner, startCity.Position);
        GetNode<UnitController>("UnitController").Init(scenario.Map, view, unit);

        var camera = GetNode<CameraController>("Camera2D");
        var bounds = MapPixelBounds(scenario.Map, view.HexSize);
        camera.Position = bounds.GetCenter();
        camera.Zoom = FitZoom(bounds, GetViewport().GetVisibleRect().Size);
        camera.MakeCurrent();

        _engine = new WorldEngine(scenario.Balance, adminSkills: new AdminSkillLoader().LoadFromDirectory(FindDataDirectory()));
        _state = GameState.FromScenario(scenario);
        _hud = GetNode<Hud>("Hud");
        _hud.NextMonthPressed += OnNextMonth;
        _hud.SetState(_state);

        _capture = OS.GetCmdlineArgs().Contains("--shot");
    }

    private WorldEngine _engine = null!;
    private GameState _state = null!;
    private Hud _hud = null!;

    private void OnNextMonth()
    {
        _state = _engine.AdvanceMonth(_state);
        _hud.SetState(_state);
    }

    private bool _capture;
    private int _frames;

    public override void _Process(double delta)
    {
        if (!_capture)
        {
            return;
        }

        if (++_frames < 5)
        {
            return;
        }

        // 스크린샷은 Godot이 리소스로 임포트하지 않도록 프로젝트 밖(리포 루트)에 저장한다.
        var projectDir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        var outPath = Path.Combine(projectDir.Parent?.FullName ?? projectDir.FullName, "shot_step4.png");
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(outPath);
        GetTree().Quit();
    }

    // 모든 타일의 픽셀 위치를 감싸는 경계(헥사 반경만큼 여백 포함).
    private static Rect2 MapPixelBounds(HexMap map, float hexSize)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var tile in map.Tiles())
        {
            var p = HexLayout.ToPixel(tile, hexSize);
            minX = Mathf.Min(minX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxX = Mathf.Max(maxX, p.X);
            maxY = Mathf.Max(maxY, p.Y);
        }

        var position = new Vector2(minX - hexSize, minY - hexSize);
        var size = new Vector2(maxX - minX + hexSize * 2f, maxY - minY + hexSize * 2f);
        return new Rect2(position, size);
    }

    // 맵 전체가 화면에 들어오도록 하는 줌.
    private static Vector2 FitZoom(Rect2 bounds, Vector2 viewport)
    {
        var zoom = Mathf.Min(viewport.X / bounds.Size.X, viewport.Y / bounds.Size.Y);
        zoom = Mathf.Clamp(zoom, 0.3f, 3f);
        return new Vector2(zoom, zoom);
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
