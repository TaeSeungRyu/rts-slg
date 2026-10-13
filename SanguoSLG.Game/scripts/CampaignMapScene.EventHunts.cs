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

    private void OpenSummonModal()
    {
        var caller = _state.Armies.FirstOrDefault(u => u.Id.Value == _selectedUnitId);
        if (caller is null) return;
        var items = _state.Treasures.Where(t => t.Faction == Player && t.EquippedBy is not null
            && (t.EquippedBy == caller.VanguardId || t.EquippedBy == caller.AdjutantId)
            && _treasureDefinitions.Any(d => d.Code == t.Code && d.Effect == "summon")).ToList();
        if (items.Count == 0) { ShowNotice("소환", "장착한 소환 아이템이 없습니다."); return; }
        HidePanels();
        var width = Mathf.Min(560, GetViewport().GetVisibleRect().Size.X * 0.85f);
        var box = DeployScaffold(width, out var scroll, out var panel);
        var heading = new HBoxContainer();
        box.AddChild(heading);
        var title = MakeLabel("소환 · 인접 위치 선택", 18, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = MakeButton("✕");
        close.Pressed += CloseModal;
        heading.AddChild(close);
        box.AddChild(MakeLabel("21일 활동 · 종료 후 60일 재사용 대기 · 동일 신수는 세력당 1마리", 12, Parchment));
        var origin = caller.RenewalPosition is { } continuous
            ? RenewalHexSpace.NearestHex(continuous) : caller.Field.Position;
        foreach (var item in items)
        {
            box.AddChild(GoldRule());
            var ready = item.ReusableDay is null || _state.Day >= item.ReusableDay;
            box.AddChild(MakeLabel($"{item.Name} · {(ready ? "소환 가능" : $"재사용까지 {item.ReusableDay - _state.Day}일")}", 14, GoldBright));
            var row = new HBoxContainer();
            box.AddChild(row);
            foreach (var position in origin.Neighbors())
            {
                var target = position;
                var button = MakeButton($"{position.Q},{position.R}");
                button.Disabled = !ready;
                button.Pressed += () =>
                {
                    var officer = item.EquippedBy;
                    CloseModal();
                    ShowConfirmWithOfficer("소환 확인", $"{item.Name}을(를) ({target.Q}, {target.R})에 소환합니다.\n다음 진행에 출현하여 21일 활동합니다.", officer, () =>
                    {
                        var result = _summons.Order(_state, caller.Id, item.Id, target);
                        if (!result.Ok) { ShowNotice("소환 불가", result.Error ?? "소환할 수 없습니다."); return; }
                        _state = result.State;
                        Redraw("소환 명령을 예약했습니다.");
                    });
                };
                row.AddChild(button);
            }
        }
        scroll.CustomMinimumSize = new(width, Mathf.Min(340, box.GetCombinedMinimumSize().Y));
        CenterAndDrag(panel, heading, width, 360, box);
    }

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
        row.AddChild(MakeLabel(treasure.Code.StartsWith("dragon") ? "龍" : treasure.Code.StartsWith("phoenix") ? "鳳" : "虎", 26, GoldBright));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        text.AddChild(MakeLabel(treasure.Name, 15, GoldBright));
        var date = treasure.AcquiredDay - 1;
        text.AddChild(MakeLabel($"{_state.StartYear + date / 360}년 {date % 360 / 30 + 1}월 {date % 30 + 1}일 획득", 11, Parchment));
        var definition = _treasureDefinitions.FirstOrDefault(x => x.Code == treasure.Code);
        text.AddChild(MakeLabel(definition is null ? "보물" : TreasureEffectText(definition), 11, new Color(Parchment, 0.7f)));
        var pending = _state.Encounters.Any(x => x.Id == treasure.EncounterId && x.RewardChoicePending);
        var equipped = treasure.EquippedBy is { } wearer
            ? _state.Generals.FirstOrDefault(g => g.Id == wearer)?.Name ?? $"장수 {wearer.Value}" : null;
        row.AddChild(MakeLabel(pending ? "보상 선택 대기" : equipped is null ? "미장착" : $"장착 · {equipped}", 12, Parchment));
        return card;
    }

    private static string TreasureEffectText(EventTreasureDefinition definition) => definition.Effect switch
    {
        "normal_attack" => $"일반 공격 피해 +{definition.Value}%",
        "melee_attack" => $"근접 일반 공격 피해 +{definition.Value}%",
        "damage_reduction" => $"받는 전투 피해 -{definition.Value}%",
        "might" => $"유효 무력 +{definition.Value} (최대 100)",
        "aptitude_floor" => "A 미만 병종 적성을 A로 보정",
        "fatal_to_wounded" => $"전투 사망자의 {definition.Value}%를 부상으로 전환",
        "summon" => "장착 장수의 출전 부대에서 신수 소환 가능",
        _ => definition.Name,
    };

    private void OpenGeneralTreasureModal(GeneralId generalId, CityId backCity,
        Action? backAction, Action? dismissAction)
    {
        _modalDismissAction = null;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var general = _state.Generals.First(g => g.Id == generalId);
        var width = Mathf.Min(580, GetViewport().GetVisibleRect().Size.X * 0.85f);
        var box = DeployScaffold(width, out var scroll, out var panel);
        var heading = new HBoxContainer();
        box.AddChild(heading);
        var title = MakeLabel($"{general.Name} · 획득 보물", 17, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        void ReturnToGeneral() => OpenGeneralDetail(generalId, backCity, backAction, dismissAction);
        var close = MakeButton("✕");
        close.Pressed += ReturnToGeneral;
        heading.AddChild(close);
        var service = new TreasureEquipmentService(_treasureDefinitions);
        var treasures = _state.Treasures.Where(t => t.Faction == Player
            && !_state.Encounters.Any(e => e.Id == t.EncounterId && e.RewardChoicePending))
            .OrderByDescending(t => t.AcquiredDay).ThenBy(t => t.Id).ToList();
        if (treasures.Count == 0) box.AddChild(MakeLabel("획득한 보물이 없습니다.", 13, Parchment));
        foreach (var treasure in treasures)
        {
            var equippedName = treasure.EquippedBy is { } wearer
                ? _state.Generals.FirstOrDefault(g => g.Id == wearer)?.Name ?? $"장수 {wearer.Value}" : null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            box.AddChild(row);
            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(info);
            info.AddChild(MakeLabel($"{treasure.Name} · {(equippedName is null ? "미장착" : $"{equippedName} 장착 중")}", 14, GoldBright));
            var definition = _treasureDefinitions.FirstOrDefault(d => d.Code == treasure.Code);
            if (definition is not null) info.AddChild(MakeLabel(TreasureEffectText(definition), 11, Parchment));
            if (treasure.EquippedBy is not null && treasure.EquippedBy != generalId) continue;
            var button = MakeButton(treasure.EquippedBy == generalId ? "장착해제" : "장착");
            var id = treasure.Id;
            button.Pressed += () =>
            {
                var result = treasure.EquippedBy == generalId
                    ? service.Unequip(_state, id) : service.Equip(_state, id, generalId);
                if (!result.Ok) { ShowNotice("보물 변경 불가", result.Error ?? "보물을 변경할 수 없습니다."); return; }
                _state = result.State;
                ReturnToGeneral();
            };
            row.AddChild(button);
        }
        scroll.CustomMinimumSize = new(width, Mathf.Min(480, box.GetCombinedMinimumSize().Y));
        CenterAndDrag(panel, heading, width, 500, box);
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
        var title = MakeLabel("신수 사냥 보상", 19, Gold);
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
            body.AddChild(MakeLabel($"{TroopName(reward.Code)} 처치 보상", 17, GoldBright));
            var encounter = _state.Encounters.FirstOrDefault(x => x.DefeatedDay is not null
                && x.RewardFaction == Player && x.Code == reward.Code && x.RewardChoicePending);
            var contributionRows = encounter?.DamageContributions ?? [];
            var totalDamage = contributionRows.Sum(x => x.Damage);
            if (totalDamage > 0)
                foreach (var contribution in contributionRows.Where(x => x.Damage > 0))
                {
                    var ratio = (double)contribution.Damage / totalDamage;
                    var experience = 200 + (int)Math.Round(400 * ratio, MidpointRounding.AwayFromZero);
                    var aptitude = 10 + (int)Math.Round(40 * ratio, MidpointRounding.AwayFromZero);
                    var leaders = string.Join("·", contribution.Generals.Select(id =>
                        _state.Generals.FirstOrDefault(g => g.Id == id)?.Name ?? $"장수 {id.Value}"));
                    body.AddChild(MakeLabel($"{leaders} · 피해 {ratio:P0} · 경험 +{experience} / 숙련 +{aptitude}", 12, Parchment));
                }
            else
            {
                body.AddChild(MakeLabel($"선봉·부관 각각 전투 경험치 +{reward.Amount}", 13, Parchment));
                body.AddChild(MakeLabel($"사용 병종 숙련 +{reward.ExtraAmount}", 13, Parchment));
            }
            body.AddChild(MakeLabel(encounter is null ? "획득 보상은 보물 창고에서 확인" : "보상 3종 중 1개 선택", 15, Gold));
            if (encounter is not null)
                foreach (var choice in _eventHunts.RewardChoices(encounter.Code))
                {
                    var selectedEncounter = encounter.Id;
                    var selectedCode = choice.Code;
                    var select = MakeButton($"{choice.Name} · {TreasureEffectText(choice)}", accent: true);
                    select.Pressed += () =>
                    {
                        var result = _eventHunts.ChooseReward(_state, selectedEncounter, Player, selectedCode);
                        if (!result.Ok) { ShowNotice("보상 선택 실패", result.Error ?? "선택할 수 없습니다."); return; }
                        _state = result.State;
                        OpenTreasureList();
                    };
                    body.AddChild(select);
                }
            var notice = MakeLabel("선택하지 않고 닫아도 보물 창고에서 다시 선택할 수 있습니다.", 11, new Color(Parchment, 0.75f));
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
            var buttons = _modalLayer.FindChildren("*", "Button", true, false).OfType<Button>().ToList();
            var textures = _modalLayer.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().ToList();
            var modal = rewards.Count == 1 && buttons.Any(button => button.Text.Contains("호부"))
                && labels.Any(label => label.Text.Contains("600")) && textures.Any(texture => texture.Texture is not null)
                && textures.All(texture => texture.Size.X <= 220);
            var imageWidths = string.Join(',', textures.Select(texture => texture.Size.X));
            buttons.First(button => button.Text.Contains("대호의 새끼")).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var chosen = _state.Treasures.Single().Code == "tiger_cub"
                && !_state.Encounters.Single().RewardChoicePending;
            var inventory = _modalLayer!.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
                .Any(card => card.HasMeta("treasure_slot") && card.GetMeta("treasure_slot").AsString() == "tiger_cub");
            var restored = SaveService.Deserialize(SaveService.Serialize(_state));
            var passed = allCitiesSpawned && appearedReport && visibleSpawn && combat && motions && modal && chosen && inventory
                && restored.Treasures.Single().Code == "tiger_cub" && TroopName("great_tiger") == "대호"
                && UnitController3D.ModelFileAt(TroopModelIndex["great_tiger"]).EndsWith("troop-great-tiger.glb");
            GD.Print($"[maptesteventhuntqa] passed={passed} allCities={allCitiesSpawned} report={appearedReport} spawn={visibleSpawn} combat={combat} motions={motions} modal={modal} chosen={chosen} inventory={inventory} save={restored.Treasures.Count} imageWidths={imageWidths}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptesteventhuntqa] {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunTreasureCloseQa()
    {
        async System.Threading.Tasks.Task Settle()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        int ModalCount() => GetChildren().OfType<CanvasLayer>()
            .Count(layer => layer.Layer == 20 && !layer.IsQueuedForDeletion());

        async System.Threading.Tasks.Task<bool> ClickClose()
        {
            await Settle();
            var close = _modalLayer?.FindChild("SystemViewClose", true, false) as Button;
            if (close is null) return false;
            var viewport = GetViewport();
            var point = close.GetGlobalRect().GetCenter();
            viewport.PushInput(new InputEventMouseMotion { Position = point }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (viewport.GuiGetHoveredControl() != close) return false;
            viewport.PushInput(new InputEventMouseButton
                { Position = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            viewport.PushInput(new InputEventMouseButton
                { Position = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await Settle();
            return _modalLayer is null && ModalCount() == 0;
        }

        try
        {
            GetViewport().NotifyMouseEntered();
            var city = _state.Cities.First(c => c.Owner == Player);
            _state = _state with
            {
                EventEncounters = [new("close-qa", "great_tiger", Player, city.Id, _state.Day,
                    DefeatedDay: _state.Day, RewardFaction: Player, RewardChoicePending: true)],
                FactionTreasures = [new("close-qa:reward", "tiger_tally", "호부", Player, _state.Day, "close-qa")],
            };
            OpenHuntRewards([new(WorldEventKind.EventUnitDefeated, Player, Code: "great_tiger")]);
            await Settle();
            _modalLayer!.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.StartsWith("대호의 새끼"))
                .EmitSignal(Button.SignalName.Pressed);
            var rewardClose = await ClickClose();

            var equipment = new TreasureEquipmentService(_treasureDefinitions);
            var general = _state.Generals.First(g => equipment.Equip(_state, "close-qa:reward", g.Id).Ok);
            OpenGeneralDetail(general.Id, city.Id);
            await Settle();
            _modalLayer!.FindChild("GeneralTreasureEquip", true, false)!
                .EmitSignal(Button.SignalName.Pressed);
            await Settle();
            var equipLayerCount = ModalCount();
            _modalLayer!.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "✕").EmitSignal(Button.SignalName.Pressed);
            await Settle();
            var equipBackClose = _modalLayer?.FindChild("GeneralTreasureEquip", true, false) is not null
                && ModalCount() == 1;
            CloseModal();

            OpenGeneralDetail(general.Id, city.Id);
            await Settle();
            _modalLayer!.FindChild("GeneralTreasureEquip", true, false)!
                .EmitSignal(Button.SignalName.Pressed);
            await Settle();
            _modalLayer!.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "장착").EmitSignal(Button.SignalName.Pressed);
            await Settle();
            var equippedClose = _state.Treasures.Single().EquippedBy == general.Id
                && _modalLayer?.FindChild("GeneralTreasureEquip", true, false) is not null;
            CloseModal();
            var passed = rewardClose && equipLayerCount == 1 && equipBackClose && equippedClose && ModalCount() == 0;
            GD.Print($"[treasure-close-qa] passed={passed} rewardClose={rewardClose} equipLayers={equipLayerCount} equipBackClose={equipBackClose} equippedClose={equippedClose} remainingLayers={ModalCount()}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[treasure-close-qa] {error}");
            GetTree().Quit(1);
        }
    }
}
