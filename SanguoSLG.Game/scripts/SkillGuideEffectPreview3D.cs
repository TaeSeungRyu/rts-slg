using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>스킬 안내 화면에서 실제 캠페인 액티브 이펙트를 작은 독립 전장에 재생한다.</summary>
public sealed partial class SkillGuideEffectPreview3D : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private Node3D? _stage;
    private MapView3D? _previewMap;
    private Node3D? _ally;
    private Node3D? _enemy;
    private UnitController3D? _allyUnit;
    private UnitController3D? _enemyUnit;
    private ActiveSkill? _skill;
    private Godot.Timer? _timer;
    private int _day;
    private readonly Label _status;

    public string? SkillCode => _skill?.Code;
    public int Day => _day;
    public bool EffectAttached { get; private set; }
    public bool HasUnitModels => _allyUnit is not null && _enemyUnit is not null;
    public int CombatMotionCount => (_allyUnit?.AttackMotionStartCount ?? 0) + (_enemyUnit?.AttackMotionStartCount ?? 0);
    public float TileCenterDistance { get; private set; }
    public float OpponentDistance => _ally is not null && _enemy is not null
        ? _ally.Position.DistanceTo(_enemy.Position) : 0f;
    public bool GreenTerrainApplied { get; private set; }
    public float CameraSize => (_stage?.FindChild("PreviewCamera", true, false) as Camera3D)?.Size ?? 0f;

    public SkillGuideEffectPreview3D()
    {
        Name = "SkillGuideEffectPreview";
        CustomMinimumSize = new Vector2(420, 278);
        Stretch = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _viewport = new SubViewport
        {
            Name = "SkillGuideBattleViewport",
            Size = new Vector2I(700, 300),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
            HandleInputLocally = false,
        };
        AddChild(_viewport);
        _status = new Label { Name = "SkillGuidePreviewStatus", Text = "액티브 스킬을 선택하세요",
            Position = new Vector2(12, 8), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_status);
    }

    public void ShowSkill(ActiveSkill? skill)
    {
        _timer?.Stop();
        _skill = skill;
        _day = 0;
        EffectAttached = false;
        if (_stage is not null)
        {
            _viewport.RemoveChild(_stage);
            _stage.QueueFree();
        }
        _stage = new Node3D { Name = "PreviewStage" };
        _viewport.AddChild(_stage);
        _previewMap = new MapView3D { Name = "PreviewUnitMap" };
        _stage.AddChild(_previewMap);
        var camera = new Camera3D { Name = "PreviewCamera", Current = true,
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 2.9f, Position = new Vector3(0, 2.9f, 4.6f) };
        _stage.AddChild(camera);
        camera.LookAt(new Vector3(0, 0.3f, 0));
        _stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -25, 0),
            LightEnergy = 2.2f });
        _stage.AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.54f, 0.70f, 0.63f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.7f, 0.72f, 0.7f),
        } });
        var grassScene = GD.Load<PackedScene>("res://assets/models/grass.glb");
        var ground = new Node3D { Name = "PreviewPlainTiles" };
        _stage.AddChild(ground);
        var probe = grassScene.Instantiate<Node3D>();
        var mesh = FindTileMesh(probe);
        var bounds = mesh!.Mesh.GetAabb();
        var flatTop = bounds.Size.X >= bounds.Size.Z;
        var hexSize = (flatTop ? bounds.Size.X : bounds.Size.Z) / 2f;
        var tileTop = bounds.End.Y;
        probe.Free();
        var sqrt3 = Mathf.Sqrt(3f);
        Vector3 HexPosition(int q, int r) => flatTop
            ? new Vector3(hexSize * 1.5f * q, 0, hexSize * sqrt3 * (r + q / 2f))
            : new Vector3(hexSize * sqrt3 * (q + r / 2f), 0, hexSize * 1.5f * r);
        TileCenterDistance = HexPosition(1, 0).Length();
        GreenTerrainApplied = true;
        foreach (var (q, r) in new[]
        {
            (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1),
        })
        {
            var forest = q == 0 && r == -1;
            var mountain = q == 1 && r == -1;
            var tileScene = forest ? GD.Load<PackedScene>("res://assets/models/grass-forest.glb")
                : mountain ? GD.Load<PackedScene>("res://assets/models/mountain-small.glb") : grassScene;
            var tile = tileScene.Instantiate<Node3D>();
            if (forest) tile.Name = "PreviewForestTile";
            if (mountain) tile.Name = "PreviewMountainTile";
            tile.Position = HexPosition(q, r);
            foreach (var tileMesh in TileMeshes(tile).Where(_ => !forest && !mountain))
            {
                tileMesh.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.39f, 0.62f, 0.34f),
                    Roughness = 1f,
                };
            }
            ground.AddChild(tile);
        }
        _ally = Unit(0, HexPosition(-1, 0) * 0.58f + Vector3.Up * tileTop, out _allyUnit);
        _enemy = Unit(1, HexPosition(1, 0) * 0.58f + Vector3.Up * tileTop, out _enemyUnit);
        _status.Text = skill is null ? "패시브 스킬은 별도 발동 에셋이 없습니다" : "1일차 · ● ○ ○ ○ ○";
        if (skill is null) return;
        _timer ??= new Godot.Timer { WaitTime = 0.7, OneShot = false };
        if (_timer.GetParent() is null)
        {
            AddChild(_timer);
            _timer.Timeout += NextDay;
        }
        _timer.Start();
    }

    private Node3D Unit(int modelIndex, Vector3 position, out UnitController3D unit)
    {
        var anchor = new Node3D { Position = position, Scale = Vector3.One * 1.12f };
        _stage!.AddChild(anchor);
        unit = new UnitController3D();
        anchor.AddChild(unit);
        unit.InitDisplay(_previewMap!, modelIndex == 0 ? new Color(0.3f, 0.58f, 0.9f)
            : new Color(0.9f, 0.35f, 0.3f), modelIndex, new SanguoSLG.Core.Spatial.HexCoord(0, 0));
        return anchor;
    }

    private static MeshInstance3D? FindTileMesh(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null) return mesh;
        foreach (var child in node.GetChildren())
            if (FindTileMesh(child) is { } found) return found;
        return null;
    }

    private static IEnumerable<MeshInstance3D> TileMeshes(Node node)
    {
        if (node is MeshInstance3D mesh) yield return mesh;
        foreach (var child in node.GetChildren())
            foreach (var nested in TileMeshes(child)) yield return nested;
    }

    private void NextDay()
    {
        if (_skill is null || _ally is null || _enemy is null) return;
        _day++;
        if (_day <= 5)
        {
            _status.Text = $"{_day}일차 · {new string('●', _day)}{new string('○', 5 - _day)}";
            _allyUnit?.PlayAttackMotionToward(_enemy.GlobalPosition);
            _enemyUnit?.PlayAttackMotionToward(_ally.GlobalPosition);
            return;
        }
        _timer?.Stop();
        _status.Text = $"6일차 · {_skill.Name} 발동";
        var target = _skill.Type is ActiveType.Defense or ActiveType.Heal ? _ally : _enemy;
        ActiveSkillPresentation.ShowCasterActivation(_ally);
        EffectAttached = ActiveSkillPresentation.AttachEffect(target, _skill, _ally.GlobalPosition);
        if (!EffectAttached)
            _status.Text = $"6일차 · {_skill.Name} (별도 3D 효과 없음)";
    }

    public override void _ExitTree() => _timer?.Stop();
}
