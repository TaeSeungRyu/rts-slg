using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>스킬 안내 화면에서 실제 캠페인 액티브 이펙트를 작은 독립 전장에 재생한다.</summary>
public sealed partial class SkillGuideEffectPreview3D : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private Node3D? _stage;
    private Node3D? _ally;
    private Node3D? _enemy;
    private ActiveSkill? _skill;
    private Godot.Timer? _timer;
    private int _day;
    private readonly Label _status;

    public string? SkillCode => _skill?.Code;
    public int Day => _day;

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
        if (_stage is not null)
        {
            _viewport.RemoveChild(_stage);
            _stage.QueueFree();
        }
        _stage = new Node3D { Name = "PreviewStage" };
        _viewport.AddChild(_stage);
        var camera = new Camera3D { Current = true, Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 6.2f, Position = new Vector3(0, 5.2f, 8.5f) };
        _stage.AddChild(camera);
        camera.LookAt(new Vector3(0, 0.4f, 0));
        _stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -25, 0),
            LightEnergy = 2.2f });
        _stage.AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.08f, 0.11f, 0.13f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.7f, 0.72f, 0.7f),
        } });
        _ally = Unit("res://assets/models/troop-swordsman.glb", new Vector3(-1.5f, 0, 0));
        _enemy = Unit("res://assets/models/troop-swordsman.glb", new Vector3(1.5f, 0, 0));
        _status.Text = skill is null ? "패시브 스킬은 별도 발동 에셋이 없습니다" : "1일차 · ● ○ ○ ○ ○";
        if (skill is null) return;
        _timer ??= new Godot.Timer { WaitTime = 0.34, OneShot = false };
        if (_timer.GetParent() is null)
        {
            AddChild(_timer);
            _timer.Timeout += NextDay;
        }
        _timer.Start();
    }

    private Node3D Unit(string path, Vector3 position)
    {
        var anchor = new Node3D { Position = position };
        _stage!.AddChild(anchor);
        if (ResourceLoader.Exists(path))
        {
            var model = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            model.Scale = Vector3.One * 0.72f;
            anchor.AddChild(model);
        }
        return anchor;
    }

    private void NextDay()
    {
        if (_skill is null || _ally is null || _enemy is null) return;
        _day++;
        if (_day <= 5)
        {
            _status.Text = $"{_day}일차 · {new string('●', _day)}{new string('○', 5 - _day)}";
            return;
        }
        _timer?.Stop();
        _status.Text = $"6일차 · {_skill.Name} 발동";
        var target = _skill.Type is ActiveType.Defense or ActiveType.Heal ? _ally : _enemy;
        ActiveSkillPresentation.ShowCasterActivation(_ally);
        if (!ActiveSkillPresentation.AttachEffect(target, _skill, _ally.GlobalPosition))
            _status.Text = $"6일차 · {_skill.Name} (별도 3D 효과 없음)";
    }

    public override void _ExitTree() => _timer?.Stop();
}
