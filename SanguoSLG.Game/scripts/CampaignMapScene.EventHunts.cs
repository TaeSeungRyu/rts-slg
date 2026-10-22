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
    private int _summonTargetingUnitId = -1;
    private string? _summonTargetingItemId;
    private readonly Dictionary<string, ImageTexture> _treasureTextures = new(StringComparer.Ordinal);
    private IReadOnlyList<AncientRelicDefinition> _ancientRelics = [];

    private ImageTexture? TreasureTexture(string code)
    {
        if (_treasureTextures.TryGetValue(code, out var cached)) return cached;
        var path = $"res://assets/ui/items/{code}.png";
        if (!Godot.FileAccess.FileExists(path)) return null;
        using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image is null || image.IsEmpty()) return null;
        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        _treasureTextures[code] = texture;
        return texture;
    }

    private PanelContainer TreasureImage(string code, int size = 64)
    {
        var panel = new PanelContainer
        {
            Name = $"TreasureImage_{code}", ClipContents = true,
            CustomMinimumSize = new(size, size),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", Frame(new Color(0.055f, 0.04f, 0.025f),
            new Color(Gold, 0.65f), 1, 7, 3));
        var texture = TreasureTexture(code);
        panel.AddChild(new TextureRect
        {
            Name = "TreasureTexture", Texture = texture,
            CustomMinimumSize = new(size - 6, size - 6),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        if (texture is null)
            panel.AddChild(MakeLabel("古", 25, GoldBright));
        return panel;
    }

    private void ApplyTreasureButtonIcon(Button button, string code)
    {
        button.Icon = TreasureTexture(code);
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("icon_max_width", 36);
        button.AddThemeConstantOverride("h_separation", 10);
        button.CustomMinimumSize = new(button.CustomMinimumSize.X, 44);
    }

    private void SeedMapTestTreasures()
    {
        var owned = _state.Treasures.Where(item => item.Faction == Player)
            .Select(item => item.Code).ToHashSet();
        _state = _state with
        {
            FactionTreasures = _state.Treasures.Concat(_treasureDefinitions
                .Where(definition => !owned.Contains(definition.Code))
                .Select(definition => new FactionTreasure($"maptest:treasure:{definition.Code}",
                    definition.Code, definition.Name, Player, _state.Day, "maptest:treasures"))).ToList(),
        };
    }

    private void RunAllTreasuresQa()
    {
        try
        {
            var count = _state.Treasures.Count;
            SeedMapTestTreasures();
            var items = _state.Treasures.Where(item => item.Faction == Player).ToList();
            var restored = SaveService.Deserialize(SaveService.Serialize(_state));
            var general = _state.Assignments.First(posting => posting.Faction == Player
                && posting.Location is not null && !_state.IsGeneralBusy(posting.General)).General;
            var equipment = new TreasureEquipmentService(_treasureDefinitions);
            var passed = count == _state.Treasures.Count && items.Count == _treasureDefinitions.Count
                && items.Select(item => item.Code).Distinct().Count() == items.Count
                && items.All(item => item.EquippedBy is null && item.ReusableDay is null
                    && restored.Treasures.Any(saved => saved == item)
                    && equipment.Equip(_state, item.Id, general).Ok);
            GD.Print($"[maptestalltreasuresqa] passed={passed} items={items.Count} equipment=true save={restored.Treasures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptestalltreasuresqa] {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunPhoenixMotionQa()
    {
        try
        {
            var token = new UnitController3D();
            AddChild(token);
            token.InitDisplay(_view, GoldBright, TroopModelIndex["phoenix"], new(0, 0));
            var body = (Node3D)token.FindChild("body", true, false)!;
            var wing = (Node3D)token.FindChild("arm_r", true, false)!;
            var restY = body.Position.Y;
            var restWing = wing.Rotation;
            token.DisplayContinuousAt(token.Position + Vector3.Right * 0.1f, true);
            for (var frame = 0; frame < 8; frame++) token._Process(0.10);
            var flying = body.Position.Y > restY + 0.25f && wing.Rotation != restWing;
            token.DisplayContinuousAt(token.Position, false);
            var target = token.GlobalPosition + Vector3.Right;
            token.PlayAttackMotionToward(target);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var fire = FindChild("PhoenixTargetFire", true, false) as Node3D;
            var burning = fire is not null && fire.GlobalPosition.DistanceTo(target) < 0.01f
                && fire.FindChild("Effect_Fire", true, false) is not null;
            await ToSignal(GetTree().CreateTimer(1.3), SceneTreeTimer.SignalName.Timeout);
            var cleaned = fire is not null && !GodotObject.IsInstanceValid(fire);
            var passed = flying && burning && cleaned;
            GD.Print($"[maptestphoenixmotionqa] passed={passed} flying={flying} targetFire={burning} cleanup={cleaned}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptestphoenixmotionqa] {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunTreasureImagesQa()
    {
        try
        {
            async System.Threading.Tasks.Task Settle()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            bool ImagesFit(int expected)
            {
                var images = _modalLayer!.FindChildren("TreasureTexture", "TextureRect", true, false)
                    .OfType<TextureRect>().ToList();
                return images.Count == expected && images.All(image => image.Texture is not null && image.Size.X > 0
                    && image.Size.Y > 0 && image.Size.X <= 64 && image.Size.Y <= 64);
            }
            var loaded = _treasureDefinitions.All(definition => TreasureTexture(definition.Code) is not null);
            OpenTreasureList();
            await Settle();
            var inventory = ImagesFit(_treasureDefinitions.Count);
            CloseModal();
            var posting = _state.Assignments.First(posting => posting.Faction == Player
                && posting.Location is not null && !_state.IsGeneralBusy(posting.General));
            OpenGeneralTreasureModal(posting.General, posting.Location!.Value, null, null);
            await Settle();
            var equipment = ImagesFit(_treasureDefinitions.Count);
            var passed = loaded && inventory && equipment;
            GD.Print($"[maptesttreasureimagesqa] passed={passed} loaded={_treasureTextures.Count} inventory={inventory} equipment={equipment}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptesttreasureimagesqa] {error}");
            GetTree().Quit(1);
        }
    }

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
        box.AddChild(MakeLabel("보물을 고른 뒤 지도에서 소환할 인접 빈 육상 칸을 클릭하세요.", 12, Parchment));
        foreach (var item in items)
        {
            box.AddChild(GoldRule());
            var ready = item.ReusableDay is null || _state.Day >= item.ReusableDay;
            box.AddChild(MakeLabel($"{item.Name} · {(ready ? "소환 가능" : $"재사용까지 {item.ReusableDay - _state.Day}일")}", 14, GoldBright));
            var button = MakeButton(ready ? $"{item.Name} · 위치 선택" : "재사용 대기");
            ApplyTreasureButtonIcon(button, item.Code);
            button.Disabled = !ready;
            button.Pressed += () =>
            {
                CloseModal();
                _summonTargetingUnitId = caller.Id.Value;
                _summonTargetingItemId = item.Id;
                _log.Text = $"{item.Name} · 지도에서 소환 위치 선택 (Esc/우클릭 취소)";
            };
            box.AddChild(button);
        }
        scroll.CustomMinimumSize = new(width, Mathf.Min(340, box.GetCombinedMinimumSize().Y));
        CenterAndDrag(panel, heading, width, 360, box);
    }

    private void StopSummonTargeting()
    {
        _summonTargetingUnitId = -1;
        _summonTargetingItemId = null;
        _hover.Visible = false;
    }

    private void SelectSummonTarget(Vector2 screen)
    {
        if (_summonTargetingUnitId < 0 || _summonTargetingItemId is not { } itemId) return;
        if (RayToGround(screen) is not { } position) return;
        var callerId = new UnitId(_summonTargetingUnitId);
        var check = _summons.Order(_state, callerId, itemId, position);
        if (!check.Ok)
        {
            _log.Text = check.Error ?? "인접한 빈 육상 위치를 선택하세요.";
            return;
        }
        var item = _state.Treasures.First(x => x.Id == itemId);
        StopSummonTargeting();
        ShowConfirmWithOfficer("소환 확인", $"{item.Name}을(를) 선택한 위치에 즉시 소환합니다.\n21일 동안 활동합니다.",
            item.EquippedBy, () =>
            {
                var result = _summons.Order(_state, callerId, itemId, position);
                if (!result.Ok) { ShowNotice("소환 불가", result.Error ?? "소환할 수 없습니다."); return; }
                _state = result.State;
                Redraw("소환수가 나타났습니다.");
            });
    }

    private string EventHuntStatus(EventEncounter encounter)
    {
        if (encounter.Unit is { } id && _state.Armies.FirstOrDefault(unit => unit.Id == id) is { } active)
            return $"활동 중 · {WildBeastRemainingDays(id)}일 · 위치 ({active.Field.Position.Q}, {active.Field.Position.R})";
        if (encounter.ExpiredDay is not null) return "출현 기간 종료";
        var remaining = encounter.ScheduledDay - _state.Day;
        if (remaining > 0) return $"출현까지 {remaining}일";
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
            NoDepthTest = true, Modulate = MapRelationTextColor(army.Field.Owner), Text = ArmyTroopLabel(army),
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
        row.AddChild(TreasureImage(treasure.Code));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        text.AddChild(MakeLabel(treasure.Name, 15, GoldBright));
        var date = treasure.AcquiredDay - 1;
        text.AddChild(MakeLabel($"{_state.StartYear + date / 360}년 {date % 360 / 30 + 1}월 {date % 30 + 1}일 획득", 11, Parchment));
        var definition = _treasureDefinitions.FirstOrDefault(x => x.Code == treasure.Code);
        if (definition?.Source == "ancient_relic")
            text.AddChild(MakeLabel("탐색 · 고대유물", 11, Gold));
        text.AddChild(MakeLabel(definition is null ? "보물" : TreasureEffectText(definition), 11, new Color(Parchment, 0.7f)));
        var pending = _state.Encounters.Any(x => x.Id == treasure.EncounterId && x.RewardChoicePending);
        var equipped = treasure.EquippedBy is { } wearer
            ? _state.Generals.FirstOrDefault(g => g.Id == wearer)?.Name ?? $"장수 {wearer.Value}" : null;
        row.AddChild(MakeLabel(pending ? "보상 선택 대기" : equipped is null ? "미장착" : $"장착 · {equipped}", 12, Parchment));
        return card;
    }

    private string TreasureEffectText(EventTreasureDefinition definition) => definition.Effect switch
    {
        "normal_attack" => $"일반 공격 피해 +{definition.Value}%",
        "melee_attack" => $"근접 일반 공격 피해 +{definition.Value}%",
        "damage_reduction" => $"받는 전투 피해 -{definition.Value}%",
        "might" => $"유효 무력 +{definition.Value} (최대 100)",
        "aptitude_floor" => "A 미만 병종 적성을 A로 보정",
        "fatal_to_wounded" => $"전투 사망자의 {definition.Value}%를 부상으로 전환",
        "summon" => "장착 장수의 출전 부대에서 신수 소환 가능",
        "attack_percent" => $"부대 공격 피해 +{definition.Value}%",
        "defense_percent" => $"부대 방어력 +{definition.Value}%",
        "aptitude_percent" => $"{_ancientRelics.FirstOrDefault(x => x.Code == definition.Code)?.TroopClass switch
        {
            "Infantry" => "보병",
            "Archer" => "궁병",
            "Cavalry" => "기병",
            "Siege" => "공성",
            _ => "해당 병종",
        }} 실효 적성 +{definition.Value}%",
        "provisions_percent" => $"담당/주최 군량 생산 +{definition.Value}%",
        "gold_percent" => $"담당/주최 금 생산 +{definition.Value}%",
        "recruit_percent" => $"담당 병력 생산 +{definition.Value}%",
        "security_percent" => $"담당 치안 증가량 +{definition.Value}%",
        _ => definition.Name,
    };

    private void OpenGeneralTreasureModal(GeneralId generalId, CityId backCity,
        Action? backAction, Action? dismissAction)
    {
        _modalDismissAction = null;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var general = _state.Generals.First(g => g.Id == generalId);
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(760, viewport.X * 0.88f);
        var height = Mathf.Min(650, viewport.Y * 0.82f);
        var box = DeployScaffold(width, out var scroll, out var panel);
        var heading = new HBoxContainer();
        box.AddChild(heading);
        var title = MakeLabel($"{general.Name} · 아이템 장착", 19, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        void ReturnToGeneral() => OpenGeneralDetail(generalId, backCity, backAction, dismissAction);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new(34, 30);
        close.Pressed += ReturnToGeneral;
        heading.AddChild(close);
        var service = new TreasureEquipmentService(_treasureDefinitions);
        var treasures = _state.Treasures.Where(t => t.Faction == Player
            && !_state.Encounters.Any(e => e.Id == t.EncounterId && e.RewardChoicePending))
            .OrderByDescending(t => t.AcquiredDay).ThenBy(t => t.Id).ToList();
        var current = treasures.FirstOrDefault(t => t.EquippedBy == generalId);
        var summary = new PanelContainer { Name = "TreasureEquipSummary" };
        summary.AddThemeStyleboxOverride("panel", Frame(new Color(0.12f, 0.085f, 0.045f), Gold, 1, 9, 12));
        box.AddChild(summary);
        var summaryRow = new HBoxContainer();
        summaryRow.AddThemeConstantOverride("separation", 14);
        summary.AddChild(summaryRow);
        if (CircularPortraitFor(generalId) is { } portrait)
        {
            summaryRow.AddChild(new TextureRect
            {
                Texture = portrait,
                CustomMinimumSize = new Vector2(72, 72),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }
        var status = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        status.AddThemeConstantOverride("separation", 5);
        summaryRow.AddChild(status);
        status.AddChild(MakeLabel(current is null ? "현재 장착 · 없음" : $"현재 장착 · {current.Name}", 16, GoldBright));
        status.AddChild(MakeLabel(current is null
            ? "아래 보물 중 하나를 선택하세요. 장수당 하나만 장착할 수 있습니다."
            : _treasureDefinitions.FirstOrDefault(d => d.Code == current.Code) is { } currentDefinition
                ? TreasureEffectText(currentDefinition) : "보물 정보 확인 필요", 12, Parchment));
        if (current is not null) summaryRow.AddChild(TreasureImage(current.Code));
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel($"획득 보물  {treasures.Count}개", 15, GoldBright));
        if (treasures.Count == 0) box.AddChild(MakeLabel("아직 획득한 보물이 없습니다.", 13, Parchment));
        var cards = new GridContainer { Name = "TreasureEquipCards", Columns = width >= 660 ? 2 : 1 };
        cards.AddThemeConstantOverride("h_separation", 10);
        cards.AddThemeConstantOverride("v_separation", 10);
        box.AddChild(cards);
        foreach (var treasure in treasures)
        {
            var equippedName = treasure.EquippedBy is { } wearer
                ? _state.Generals.FirstOrDefault(g => g.Id == wearer)?.Name ?? $"장수 {wearer.Value}" : null;
            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(cards.Columns == 2 ? (width - 40) / 2 : width - 20, 132),
            };
            card.SetMeta("treasure_code", treasure.Code);
            card.AddThemeStyleboxOverride("panel", Frame(new Color(0.09f, 0.065f, 0.035f),
                treasure.EquippedBy == generalId ? GoldBright : new Color(Gold, 0.55f), 1, 8, 10));
            cards.AddChild(card);
            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 5);
            card.AddChild(content);
            var top = new HBoxContainer();
            top.AddThemeConstantOverride("separation", 9);
            content.AddChild(top);
            top.AddChild(TreasureImage(treasure.Code));
            var itemTitle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            top.AddChild(itemTitle);
            itemTitle.AddChild(MakeLabel(treasure.Name, 15, GoldBright));
            itemTitle.AddChild(MakeLabel(equippedName is null ? "장착 가능"
                : treasure.EquippedBy == generalId ? "현재 장착 중" : $"{equippedName} 사용 중", 11, Parchment));
            var definition = _treasureDefinitions.FirstOrDefault(d => d.Code == treasure.Code);
            if (definition is not null)
            {
                var effect = MakeLabel(TreasureEffectText(definition), 11, Parchment);
                effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                content.AddChild(effect);
            }
            var button = MakeButton(treasure.EquippedBy == generalId ? "장착 해제"
                : treasure.EquippedBy is not null ? "다른 장수 사용 중"
                : current is null ? "장착" : "교체");
            button.Name = "TreasureEquipAction";
            button.Disabled = treasure.EquippedBy is not null && treasure.EquippedBy != generalId;
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            var id = treasure.Id;
            button.Pressed += () =>
            {
                var work = _state;
                if (treasure.EquippedBy != generalId && current is not null)
                {
                    var removed = service.Unequip(work, current.Id);
                    if (!removed.Ok) { ShowNotice("보물 변경 불가", removed.Error ?? "보물을 변경할 수 없습니다."); return; }
                    work = removed.State;
                }
                var result = treasure.EquippedBy == generalId
                    ? service.Unequip(work, id) : service.Equip(work, id, generalId);
                if (!result.Ok) { ShowNotice("보물 변경 불가", result.Error ?? "보물을 변경할 수 없습니다."); return; }
                _state = result.State;
                ReturnToGeneral();
            };
            content.AddChild(button);
        }
        scroll.CustomMinimumSize = new(width, Mathf.Min(height, box.GetCombinedMinimumSize().Y));
        CenterAndDrag(panel, heading, width, height, box);
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
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
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
                    ApplyTreasureButtonIcon(select, choice.Code);
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
            var phoenixModel = GD.Load<PackedScene>(UnitController3D.ModelFileAt(TroopModelIndex["phoenix"]));
            var phoenixInstance = phoenixModel?.Instantiate();
            var phoenixAsset = phoenixInstance?.FindChild("body", true, false) is not null;
            phoenixInstance?.Free();
            var beastCards = Godot.FileAccess.FileExists("res://assets/ui/troops/thumbnails/eastern_dragon.png")
                && Godot.FileAccess.FileExists("res://assets/ui/troops/thumbnails/phoenix.png");
            var phoenixLabel = ArmyTroopLabel(tiger with
            {
                TroopCode = "phoenix",
                Pool = new TroopPool(40000, 0),
            }).StartsWith("봉황 · ", System.StringComparison.Ordinal)
                && ArmyTroopLabel(tiger with { TroopCode = "phoenix", Pool = new TroopPool(40000, 0) })
                    .Contains("일\n체력 40,000", System.StringComparison.Ordinal);
            var passed = allCitiesSpawned && appearedReport && visibleSpawn && combat && motions && modal && chosen && inventory
                && phoenixAsset && beastCards && phoenixLabel
                && restored.Treasures.Single().Code == "tiger_cub" && TroopName("great_tiger") == "대호"
                && UnitController3D.ModelFileAt(TroopModelIndex["great_tiger"]).EndsWith("troop-great-tiger.glb");
            GD.Print($"[maptesteventhuntqa] passed={passed} allCities={allCitiesSpawned} report={appearedReport} spawn={visibleSpawn} combat={combat} motions={motions} modal={modal} chosen={chosen} inventory={inventory} phoenixModel={phoenixAsset} phoenixLabel={phoenixLabel} beastCards={beastCards} save={restored.Treasures.Count} imageWidths={imageWidths}");
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
            _state = _state with { FactionTreasures = _state.Treasures.Append(
                new FactionTreasure("close-qa:second", "tiger_fang", "호아", Player,
                    _state.Day, "close-qa-second")).ToList() };
            OpenGeneralDetail(general.Id, city.Id);
            _modalLayer!.FindChild("GeneralTreasureEquip", true, false)!
                .EmitSignal(Button.SignalName.Pressed);
            await Settle();
            var cards = _modalLayer?.FindChild("TreasureEquipCards", true, false) as GridContainer;
            var summary = _modalLayer?.FindChild("TreasureEquipSummary", true, false);
            var cardLayout = cards?.Columns == 2 && cards.GetChildCount() == 2 && summary is not null;
            _modalLayer!.FindChildren("TreasureEquipAction", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "교체").EmitSignal(Button.SignalName.Pressed);
            await Settle();
            var swapped = _state.Treasures.Single(t => t.Id == "close-qa:second").EquippedBy == general.Id
                && _state.Treasures.Single(t => t.Id == "close-qa:reward").EquippedBy is null;
            CloseModal();
            OpenTreasureList();
            await Settle();
            var heading = _modalLayer?.FindChild("TreasureInventoryHeading", true, false) as MarginContainer;
            var inventoryOnly = heading?.GetThemeConstant("margin_top") == 14
                && _modalLayer!.FindChildren("*", "Button", true, false).OfType<Button>()
                    .All(button => button.Text is not ("장착" or "장착해제"))
                && _modalLayer.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.Text == $"장착 · {general.Name}");
            CloseModal();
            var passed = rewardClose && equipLayerCount == 1 && equipBackClose && equippedClose
                && cardLayout && swapped && inventoryOnly && ModalCount() == 0;
            GD.Print($"[treasure-close-qa] passed={passed} rewardClose={rewardClose} equipLayers={equipLayerCount} equipBackClose={equipBackClose} equippedClose={equippedClose} cards={cardLayout} swapped={swapped} remainingLayers={ModalCount()}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[treasure-close-qa] {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunSummonTargetQa()
    {
        try
        {
            var general = _state.Generals.First(g => _state.Assignments.Any(p => p.General == g.Id && p.Faction == Player));
            var position = new HexCoord(-7, 0);
            var unit = new CombatUnit(new FieldUnit(new(99998), Player, position, 2, 2, 1,
                    MovementDomain.Land, UnitMode.Standby, null, 99998),
                new CombatStats(10000, 10, 10), new TroopPool(10000, 0), UnitCombatState.Create(0),
                TroopCode: "swordsman", VanguardId: general.Id,
                RenewalPosition: RenewalHexSpace.Center(position));
            _state = _state with
            {
                FieldArmies = [unit],
                FactionTreasures = [new("summon-target-qa", "tiger_cub", "대호의 새끼", Player,
                    _state.Day, "summon-target-qa", general.Id)],
            };
            var target = position.Neighbors().First(hex => _summons.Order(_state, unit.Id,
                "summon-target-qa", hex).Ok);
            _selectedUnitId = unit.Id.Value;
            OpenSummonModal();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var choice = _modalLayer!.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.Contains("위치 선택"));
            choice.EmitSignal(Button.SignalName.Pressed);
            var mapTargeting = _summonTargetingUnitId == unit.Id.Value && _summonTargetingItemId == "summon-target-qa"
                && _modalLayer is null;
            var screen = _camera.UnprojectPosition(_view.HexToWorld(target)
                + new Vector3(0f, _view.TileTopY, 0f));
            GetViewport().NotifyMouseEntered();
            GetViewport().PushInput(new InputEventMouseButton
                { Position = screen, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { Position = screen, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var confirm = _confirmLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .SingleOrDefault(button => button.Text == "확인");
            confirm?.EmitSignal(Button.SignalName.Pressed);
            var summoned = _state.Armies.SingleOrDefault(army => army.IsSummon);
            var passed = mapTargeting && confirm is not null && summoned?.Field.Position == target
                && _state.SummonOrders.Count == 0 && _summonTargetingUnitId < 0;
            GD.Print($"[maptestsummontargetqa] passed={passed} map={mapTargeting} confirm={confirm is not null} summoned={summoned?.Id.Value}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[maptestsummontargetqa] {error}");
            GetTree().Quit(1);
        }
    }
}
