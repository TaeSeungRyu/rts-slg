using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private EventHuntService _eventHunts = null!;
    private readonly Dictionary<int, CombatUnit> _eventSpawnPlayback = new();
    private readonly List<WorldEvent> _pendingHuntRewards = new();

    private string EventHuntStatus(EventEncounter encounter)
    {
        if (encounter.Unit is { } id && _state.Armies.FirstOrDefault(unit => unit.Id == id) is { } active)
            return $"활동 중 · 위치 ({active.Field.Position.Q}, {active.Field.Position.R})";
        var remaining = encounter.ScheduledDay - _state.Day;
        if (remaining > 0) return $"출현까지 {remaining}일";
        if (_state.Armies.Any(unit => unit.Field.Owner == EventHuntService.EventFaction && unit.Pool.Active > 0))
            return "다른 대호가 활동 중 · 처치 후 출현";
        if (_eventHunts.FindSpawnPosition(_state, encounter) is null) return "주변의 빈 육상 출현 장소 확보 대기";
        return "출현 준비 완료 · 다음 진행에 출현";
    }

    private void LogEventHuntState(GameState state)
    {
        foreach (var encounter in state.Encounters)
        {
            var unit = state.Armies.FirstOrDefault(unit => unit.Id == encounter.Unit);
            var candidate = encounter.Unit is null ? _eventHunts.FindSpawnPosition(state, encounter) : null;
            Dbg($"[event-hunt] day={state.Day} city={encounter.DiscoveryCity.Value} scheduled={encounter.ScheduledDay} "
                + $"unit={encounter.Unit?.Value} pos={unit?.Field.Position} candidate={candidate} defeated={encounter.DefeatedDay}");
        }
    }

    private void SeedEventHuntSample()
    {
        var city = _state.Cities.First(city => city.Owner == Player && !city.IsPort);
        var general = _state.GeneralsAt(city.Id).First();
        _state = _state with { ExplorationDiscoveries = _state.Discoveries.Append(
            new(_state.Day, Player, city.Id, general, ExplorationResultKind.DivineBeast,
                "divine_beast_trace", Text: "대호 출현 검수용 확정 단서")).ToList() };
        _state = _eventHunts.ScheduleDiscoveries(_state);
        _state = _state with { EventEncounters = _state.Encounters.Select(encounter =>
            encounter.Unit is null ? encounter with { ScheduledDay = _state.Day } : encounter).ToList() };
    }

    private void EnsureArmyToken(CombatUnit army)
    {
        if (_armyTokens.ContainsKey(army.Id.Value)) return;
        var color = ArmyFactionColor(army.Field.Owner);
        var token = new UnitController3D();
        AddChild(token);
        token.InitDisplay(_view, color,
            army.IsArmyGroup ? UnitController3D.ArmyGroupModelIndex
                : army.IsSupply ? UnitController3D.SupplyModelIndex
                : TroopModelIndex.GetValueOrDefault(army.TroopCode, 0), army.Field.Position);
        token.SetFormationSize(army.IsSupply || army.IsArmyGroup || army.Field.Owner == EventHuntService.EventFaction
            ? 1 : FormationFor(army.Pool.Active));
        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 36, OutlineSize = 10,
            PixelSize = CommanderPortraitView3D.LabelPixelSize,
            NoDepthTest = true, Modulate = color, Text = ArmyTroopLabel(army),
            Position = _view.HexToWorld(army.Field.Position)
                + new Vector3(0f, _view.TileTopY + CommanderPortraitView3D.TroopLabelHeightOffset, 0f),
        };
        AddChild(label);
        _armyTokens[army.Id.Value] = token;
        _armyLabels[army.Id.Value] = label;
        var gauge = new ActiveSkillGaugeView3D { Visible = false };
        token.AddChild(gauge);
        _activeGauges[army.Id.Value] = gauge;
        var portrait = new CommanderPortraitView3D { Visible = false };
        token.AddChild(portrait);
        if (army.VanguardId is { } commander) portrait.SetPortrait(CircularPortraitFor(commander));
        _commanderPortraits[army.Id.Value] = portrait;
    }

    private PanelContainer BuildTreasureCard(FactionTreasure treasure)
    {
        var card = new PanelContainer();
        card.SetMeta("treasure_slot", treasure.Code);
        card.AddThemeStyleboxOverride("panel", Frame(new Color(0.11f, 0.075f, 0.025f), Gold, 1, 8, 10));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        card.AddChild(row);
        row.AddChild(MakeLabel("虎", 26, GoldBright));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        text.AddChild(MakeLabel($"{treasure.Name} · 사냥 증표", 15, GoldBright));
        var date = treasure.AcquiredDay - 1;
        text.AddChild(MakeLabel($"{_state.StartYear + date / 360}년 {date % 360 / 30 + 1}월 {date % 30 + 1}일 획득", 11, Parchment));
        text.AddChild(MakeLabel("세력 공용 보물 · 장착 효과는 후속 설계", 11, new Color(Parchment, 0.7f)));
        return card;
    }

    private void ShowPendingHuntRewards()
    {
        if (_pendingHuntRewards.Count == 0) return;
        var rewards = _pendingHuntRewards.ToList();
        _pendingHuntRewards.Clear();
        OpenHuntRewards(rewards);
    }

    private void OpenHuntRewards(IReadOnlyList<WorldEvent> rewards)
    {
        _modalDismissAction = null;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(720, viewport.X * 0.82f);
        var height = Mathf.Min(560, viewport.Y * 0.8f);
        var box = DeployScaffold(width, out var scroll, out var panel);
        _modalLayer!.Name = "EventHuntRewardsModal";
        var heading = new HBoxContainer();
        box.AddChild(heading);
        var title = MakeLabel("대호 사냥 성공", 19, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new(36, 30);
        close.Pressed += CloseModal;
        heading.AddChild(close);
        box.AddChild(GoldRule());
        foreach (var reward in rewards)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            box.AddChild(row);
            var image = UnitCardPanel(reward.Code);
            image.CustomMinimumSize = new(180, 120);
            image.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            image.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var texture = image.GetChild<TextureRect>(0);
            texture.CustomMinimumSize = new(180, 120);
            row.AddChild(image);
            var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 8);
            row.AddChild(body);
            body.AddChild(MakeLabel("대호 처치 보상", 17, GoldBright));
            body.AddChild(MakeLabel($"선봉·부관 각각 전투 경험치 +{reward.Amount}", 13, Parchment));
            body.AddChild(MakeLabel($"사용 병종 숙련 +{reward.ExtraAmount}", 13, Parchment));
            body.AddChild(MakeLabel("호부 1개 · 세력 보관함에 등록", 15, Gold));
            var notice = MakeLabel("기존 숙련 상한 적용 · 호부 장착 효과는 후속 설계", 11, new Color(Parchment, 0.75f));
            notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(notice);
        }
        box.AddChild(GoldRule());
        var confirm = MakeButton("확인", accent: true);
        confirm.CustomMinimumSize = new(0, 36);
        confirm.Pressed += CloseModal;
        box.AddChild(confirm);
        scroll.CustomMinimumSize = new(width, Math.Min(box.GetCombinedMinimumSize().Y, height));
        CenterAndDrag(panel, heading, width, height, box);
    }

    private async void RunEventHuntQa()
    {
        try
        {
            _state = _state with { FieldArmies = [], ExplorationDiscoveries = [], EventEncounters = [], FactionTreasures = [] };
            SeedEventHuntSample();
            var initial = _state;
            var allCitiesSpawned = true;
            foreach (var city in initial.Cities.Where(city => city.Owner == Player))
            {
                var local = initial with { ExplorationDiscoveries = [new(initial.Day, Player, city.Id, new(1),
                    ExplorationResultKind.DivineBeast, "divine_beast_trace")], EventEncounters = [], FieldArmies = [] };
                local = _eventHunts.ScheduleDiscoveries(local);
                var spawnDay = local.Encounters.Single().ScheduledDay;
                local = _eventHunts.SpawnDue(local, spawnDay, out _);
                var localTiger = local.Armies.FirstOrDefault(unit => unit.Field.Owner == EventHuntService.EventFaction);
                var success = localTiger is not null
                    && TerrainRules.CanEnter(MovementDomain.Land, _map.TerrainAt(localTiger.Field.Position));
                allCitiesSpawned &= success;
                GD.Print($"[event-spawn-location-qa] city={city.Name} port={city.IsPort} passed={success} position={localTiger?.Field.Position}");
            }
            var appeared = _engine.AdvanceWeek(initial, out var spawnTurns, out var spawnSieges, out var spawnCaptures);
            var tiger = appeared.Armies.Single(unit => unit.Field.Owner == EventHuntService.EventFaction);
            var appearedReport = _engine.LastWorldEvents.Any(e => e.Kind == WorldEventKind.EventUnitAppeared);
            _pendingState = appeared;
            BuildAnimation(new Dictionary<int, HexCoord>(), spawnTurns, spawnSieges, spawnCaptures, initial);
            _animT = 0;
            _animDeploymentIdx = _animUnitSnapshotIdx = 0;
            _advancing = true;
            _Process(0.001);
            var visibleSpawn = _armyTokens.TryGetValue(tiger.Id.Value, out var token) && token.Visible
                && _armyLabels[tiger.Id.Value].Visible && _armyLabels[tiger.Id.Value].Text.Contains("대호");
            _advancing = false;
            var general = _state.GeneralsAt(initial.Cities.First(city => city.Owner == Player && !city.IsPort).Id).First();
            var nearby = tiger.Field.Position + new HexCoord(-1, 0);
            var attacker = new CombatUnit(new FieldUnit(new(99991), Player, nearby, 0, 3, 1,
                MovementDomain.Land, UnitMode.Attack, tiger.Field.Position, 1, AssignedUnitTarget: tiger.Id),
                new(100000, 100, 100), new(100000, 0), UnitCombatState.Create(70),
                TroopCode: "swordsman", VanguardId: general, RenewalPosition: RenewalHexSpace.Center(nearby));
            var huntStart = appeared with { FieldArmies = [tiger, attacker] };
            var hunted = _engine.AdvanceWeek(huntStart, out var fightTurns, out var fightSieges, out var fightCaptures);
            var rewards = _engine.LastWorldEvents.Where(e => e.Kind == WorldEventKind.EventUnitDefeated && e.Faction == Player).ToList();
            _pendingState = hunted;
            BuildAnimation(huntStart.Armies.ToDictionary(unit => unit.Id.Value, unit => unit.Field.Position),
                fightTurns, fightSieges, fightCaptures, huntStart);
            var motions = _animAttacks.Any(attack => attack.UnitId == tiger.Id.Value)
                && _animKills.Any(kill => kill.UnitId == tiger.Id.Value);
            _state = hunted;
            var combat = fightTurns.Any(turn => turn.Combat is not null
                && turn.Combat.DamageTaken.GetValueOrDefault(tiger.Id) > 0);
            OpenHuntRewards(rewards);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var labels = _modalLayer!.FindChildren("*", "Label", true, false).OfType<Label>().ToList();
            var textures = _modalLayer.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().ToList();
            var modal = rewards.Count == 1 && labels.Any(label => label.Text.Contains("호부"))
                && labels.Any(label => label.Text.Contains("250")) && textures.Any(texture => texture.Texture is not null)
                && textures.All(texture => texture.Size.X <= 220);
            var imageWidths = string.Join(',', textures.Select(texture => texture.Size.X));
            CloseModal();
            OpenTreasureList();
            var inventory = _modalLayer!.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
                .Any(card => card.HasMeta("treasure_slot") && card.GetMeta("treasure_slot").AsString() == "tiger_tally");
            var restored = SaveService.Deserialize(SaveService.Serialize(hunted));
            var passed = allCitiesSpawned && appearedReport && visibleSpawn && combat && motions && modal && inventory
                && restored.Treasures.Count == 1 && TroopName("great_tiger") == "대호"
                && UnitController3D.ModelFileAt(TroopModelIndex["great_tiger"]).EndsWith("troop-great-tiger.glb");
            GD.Print($"[maptesteventhuntqa] passed={passed} allCities={allCitiesSpawned} report={appearedReport} spawn={visibleSpawn} combat={combat} motions={motions} modal={modal} inventory={inventory} save={restored.Treasures.Count} imageWidths={imageWidths}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptesteventhuntqa] {error}");
            GetTree().Quit(1);
        }
    }
}
