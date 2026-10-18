using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private bool _stratMapTargeting;
    private CityId _stratOrigin;
    private int _stratCommandIndex;
    private GeneralId _stratOfficer;
    private int _stratOption;
    private readonly List<MeshInstance3D> _stratTargetMarkers = new();
    private CanvasLayer? _stratHintLayer;
    private Label? _stratHint;

    private void BeginStratagemMapTargeting(CityId origin, int commandIndex, GeneralId officer, int option)
    {
        CloseModal();
        _stratOrigin = origin;
        _stratCommandIndex = commandIndex;
        _stratOfficer = officer;
        _stratOption = option;
        _stratMapTargeting = true;
        HidePanels();
        foreach (var city in _state.Cities.Where(c => c.Owner != Player && IsKnownCityLocation(c)))
        {
            var marker = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.47f, OuterRadius = 0.55f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = GoldBright,
                    EmissionEnabled = true,
                    Emission = Gold,
                    EmissionEnergyMultiplier = 1.5f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            };
            AddChild(marker);
            marker.Position = _view.HexToWorld(city.Position) + new Vector3(0, _view.TileTopY + 0.09f, 0);
            _stratTargetMarkers.Add(marker);
        }
        _stratHintLayer = new CanvasLayer { Layer = 18 };
        AddChild(_stratHintLayer);
        _stratHint = MakeLabel("금색 표시가 있는 적 성을 선택하세요. Esc 또는 우클릭으로 취소", 16, GoldBright);
        _stratHint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _stratHint.Position = new Vector2(24, 105);
        _stratHintLayer.AddChild(_stratHint);
    }

    private void SelectStratagemMapTarget(Vector2 screenPosition)
    {
        var hex = RayToGround(screenPosition);
        var city = hex is { } tile ? CityAtHex(tile) : null;
        if (city is null || city.Owner == Player || !IsKnownCityLocation(city))
        {
            if (_stratHint is not null) _stratHint.Text = "대상 선택 불가 · 확인 가능한 적 성을 선택하세요.";
            return;
        }
        var origin = _state.Cities.FirstOrDefault(c => c.Id == _stratOrigin && c.Owner == Player);
        if (origin is null || !_state.GeneralsAt(origin.Id).Contains(_stratOfficer))
        {
            FinishStratagemMapTargeting(true);
            ShowNotice("계략 실행 불가", "원래 성 또는 수행 장수의 상태가 바뀌었습니다.");
            return;
        }
        if (_state.AreAllied(Player, city.Owner) && _stratOption != 0)
        {
            if (_stratHint is not null) _stratHint.Text = $"{city.Name} · 동맹 성에는 정찰만 가능합니다.";
            return;
        }
        var commandIndex = _stratCommandIndex;
        var officer = _stratOfficer;
        var option = _stratOption;
        FinishStratagemMapTargeting(false);
        _stratTarget = city.Id;
        AskExecute(origin.Id, commandIndex, officer, option);
    }

    private void FinishStratagemMapTargeting(bool cancelled)
    {
        _stratMapTargeting = false;
        foreach (var marker in _stratTargetMarkers) if (IsInstanceValid(marker)) marker.QueueFree();
        _stratTargetMarkers.Clear();
        if (_stratHintLayer is not null) _stratHintLayer.QueueFree();
        _stratHintLayer = null;
        _stratHint = null;
        if (cancelled)
        {
            _stratTarget = null;
            SelectCity(_stratOrigin);
        }
    }

    private void RunStratagemMapTargetQa()
    {
        var origin = _state.Cities.First(city => city.Owner == Player);
        var enemy = _state.Cities.First(city => city.Owner != Player);
        _visibleTiles = _visibleTiles.Append(enemy.Position).ToHashSet();
        var commandIndex = System.Array.FindIndex(Cmds,
            command => command.Kind == SanguoSLG.Core.Domain.CommandKind.CityStratagem);
        BeginStratagemMapTargeting(origin.Id, commandIndex, _state.GeneralsAt(origin.Id).First(), 0);
        var eligible = _state.Cities.Count(city => city.Owner != Player && IsKnownCityLocation(city));
        var passed = _stratMapTargeting && _stratTargetMarkers.Count == eligible && eligible > 0;
        FinishStratagemMapTargeting(false);
        passed &= !_stratMapTargeting && _stratTargetMarkers.Count == 0;
        GD.Print($"[mapteststrattargetqa] eligible={eligible} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
