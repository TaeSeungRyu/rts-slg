namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed partial class CampaignMapScene
{
    private void OpenBatchDeployCompose(CityId city, List<BatchDeployDraft>? existing = null)
    {
        _depModalCity = city;
        if (existing is null) _depDelayDays = 0;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }

        var reservedTroops = ReservedTroopsByCode(city, -1, editingSupply: false);
        var reservedGenerals = ReservedDeployGenerals(-1, editingSupply: false);
        var availableIds = _state.GeneralsAt(city)
            .Where(id => !_state.IsGeneralBusy(id) && !reservedGenerals.Contains(id))
            .OrderBy(id => id.Value)
            .ToList();
        var planner = new BatchDeployPlanner();
        var drafts = existing ?? planner.Recommend(city, _state.Garrisons, _state.Generals,
            availableIds, _troops, DeployMaxTroopsFor(city), reservedGenerals, reservedTroops).ToList();

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.84f, 760f, 1080f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var cityName = _state.Cities.First(c => c.Id == city).Name;
        var title = MakeLabel($"◈  일괄전투편성   《 {cityName} 》  ⠿", 18, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var back = MakeButton("◀ 출전 목록");
        back.Pressed += OpenDeployHub;
        titleRow.AddChild(back);
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("병종별 10,000명을 우선 추천합니다. 선봉을 먼저 배치한 뒤 남은 장수를 부관으로 배치합니다.", 12, Parchment));

        var table = new GridContainer { Columns = 7, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 7);
        table.AddThemeConstantOverride("v_separation", 4);
        foreach (var header in new[] { "부대", "장수 1 (선봉)", "장수 2 (부관)", "병종", "병력수", "휴대 금", "" })
        {
            var label = MakeLabel(header, 12, GoldBright);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            table.AddChild(label);
        }
        box.AddChild(table);

        var rowErrors = new List<Label>();
        Button? apply = null;
        var summary = MakeLabel("", 12, Parchment);
        void Validate()
        {
            var validation = planner.Validate(drafts, city, _state.Garrisons, availableIds,
                DeployMaxTroopsFor(city), reservedTroops, reservedGenerals);
            var goldValid = drafts.Sum(draft => draft.Gold) <= _state.Cities.First(c => c.Id == city).Gold;
            for (var i = 0; i < rowErrors.Count; i++)
                rowErrors[i].Text = validation.RowErrors.GetValueOrDefault(i, "");
            summary.Text = drafts.Count == 0
                ? "⚠ 편성 가능한 병력과 장수가 없습니다."
                : validation.Ok && goldValid ? $"{drafts.Count}개 부대 초안 · 확인 후 기존 전투편성 목록에서 목표를 지정합니다."
                : !goldValid ? "⚠ 부대에 나눈 휴대 금의 합계가 도시 보유 금을 초과했습니다."
                : "⚠ " + string.Join("  /  ", validation.RowErrors.OrderBy(pair => pair.Key)
                    .Select(pair => $"부대 {pair.Key + 1}: {pair.Value}"));
            if (apply is not null) apply.Disabled = !validation.Ok || !goldValid;
        }

        for (var rowIndex = 0; rowIndex < drafts.Count; rowIndex++)
        {
            var index = rowIndex;
            var draft = drafts[index];
            var troop = _troops.First(t => t.Code == draft.TroopCode);
            var unitCell = new VBoxContainer { CustomMinimumSize = new Vector2(54, 52) };
            unitCell.AddChild(DeployUnitArtwork(UnitCard(troop.Code, troop.Class), 34));
            var number = MakeLabel($"{index + 1}", 11, GoldBright);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            unitCell.AddChild(number);
            table.AddChild(unitCell);

            OptionButton GeneralOption(GeneralId? selected, bool allowNone)
            {
                var option = MakeOption(220);
                if (allowNone)
                {
                    option.AddItem("없음");
                    option.SetItemMetadata(0, 0);
                }
                foreach (var id in availableIds)
                {
                    var general = _state.Generals.First(g => g.Id == id);
                    option.AddIconItem(OfficerTablePortraitFor(id), $"{general.Name} · {CurrentDuty(id)} · {GradeText(general.AptitudeFor(troop.Class))} · 무{general.Might}");
                    option.SetItemMetadata(option.ItemCount - 1, id.Value);
                }
                for (var item = 0; item < option.ItemCount; item++)
                    if (option.GetItemMetadata(item).AsInt32() == selected?.Value) option.Select(item);
                return option;
            }

            var vanguard = GeneralOption(draft.Vanguard, false);
            vanguard.ItemSelected += selected =>
            {
                drafts[index] = drafts[index] with { Vanguard = new GeneralId(vanguard.GetItemMetadata((int)selected).AsInt32()) };
                Validate();
            };
            table.AddChild(vanguard);
            var adjutant = GeneralOption(draft.Adjutant, true);
            adjutant.ItemSelected += selected =>
            {
                var value = adjutant.GetItemMetadata((int)selected).AsInt32();
                drafts[index] = drafts[index] with { Adjutant = value == 0 ? null : new GeneralId(value) };
                Validate();
            };
            table.AddChild(adjutant);

            var troopOption = MakeOption(112);
            troopOption.Name = $"BatchDeployTroopType{index}";
            troopOption.FitToLongestItem = false;
            troopOption.SetMeta("batch_compact_column", true);
            foreach (var garrison in _state.Garrisons.Where(g => g.City == city && !g.Trainee && g.Troops > 0)
                         .OrderBy(g => g.TroopCode, StringComparer.Ordinal))
            {
                var template = _troops.FirstOrDefault(t => t.Code == garrison.TroopCode);
                if (template is null) continue;
                troopOption.AddItem(template.Name);
                troopOption.SetItemMetadata(troopOption.ItemCount - 1, template.Code);
                if (template.Code == draft.TroopCode) troopOption.Select(troopOption.ItemCount - 1);
            }
            troopOption.ItemSelected += selected =>
            {
                drafts[index] = drafts[index] with { TroopCode = troopOption.GetItemMetadata((int)selected).AsString() };
                ReopenBatchDeployCompose(city.Value, drafts);
            };
            table.AddChild(troopOption);

            var amount = ApplyNumberInputStyle(new SpinBox
            {
                Name = $"BatchDeployTroops{index}",
                MinValue = 1,
                MaxValue = DeployMaxTroopsFor(city),
                Step = 100,
                Value = draft.Troops,
                CustomMinimumSize = new Vector2(92, 34),
            });
            amount.SetMeta("batch_compact_column", true);
            amount.ValueChanged += value => { drafts[index] = drafts[index] with { Troops = (int)value }; Validate(); };
            table.AddChild(amount);
            var gold = ApplyNumberInputStyle(new SpinBox
            {
                Name = $"BatchDeployGold{index}",
                MinValue = 0,
                MaxValue = _state.Cities.First(c => c.Id == city).Gold,
                Step = 100,
                Value = draft.Gold,
                CustomMinimumSize = new Vector2(92, 34),
            });
            gold.SetMeta("deploy_gold_selector", true);
            gold.SetMeta("batch_compact_column", true);
            gold.ValueChanged += value => { drafts[index] = drafts[index] with { Gold = (int)value }; Validate(); };
            table.AddChild(gold);
            var remove = MakeButton("삭제");
            remove.Name = $"BatchDeployRemove{index}";
            remove.CustomMinimumSize = new Vector2(56, 34);
            remove.SetMeta("batch_compact_column", true);
            remove.Pressed += () => { drafts.RemoveAt(index); ReopenBatchDeployCompose(city.Value, drafts); };
            table.AddChild(remove);

            var error = MakeLabel("", 11, new Color(0.95f, 0.48f, 0.42f));
            error.CustomMinimumSize = Vector2.Zero;
            rowErrors.Add(error);
        }

        box.AddChild(GoldRule());
        AddDeploymentDelaySelector(box, _depDelayDays, value => _depDelayDays = value);
        box.AddChild(summary);
        apply = MakeButton("▶ 편성 초안 적용", accent: true);
        apply.CustomMinimumSize = new Vector2(0, 36);
        apply.Pressed += () => ApplyBatchDeployDrafts(city, drafts);
        box.AddChild(apply);
        Validate();
        var contentH = box.GetCombinedMinimumSize().Y;
        var fittedHeight = Mathf.Clamp(contentH + 8f, 420f, vp.Y - 24f);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.CustomMinimumSize = new Vector2(mw, fittedHeight);
        scroll.SetMeta("batch_modal_scroll_disabled", true);
        scroll.SetMeta("batch_content_fitted_height", contentH);
        CenterAndDrag(panel, titleRow, mw, fittedHeight, box);
    }

    private void ReopenBatchDeployCompose(int cityId, List<BatchDeployDraft> drafts)
        => OpenBatchDeployCompose(new CityId(cityId), drafts);

    private void ApplyBatchDeployDrafts(CityId city, IReadOnlyList<BatchDeployDraft> drafts)
    {
        var planner = new BatchDeployPlanner();
        var reservedTroops = ReservedTroopsByCode(city, -1, editingSupply: false);
        var reservedGenerals = ReservedDeployGenerals(-1, editingSupply: false);
        var availableIds = _state.GeneralsAt(city)
            .Where(id => !_state.IsGeneralBusy(id) && !reservedGenerals.Contains(id))
            .ToList();
        var validation = planner.Validate(drafts, city, _state.Garrisons, availableIds,
            DeployMaxTroopsFor(city), reservedTroops, reservedGenerals);
        if (!validation.Ok)
        {
            OpenBatchDeployCompose(city, drafts.ToList());
            return;
        }

        var ids = drafts.SelectMany(draft => new[] { (GeneralId?)draft.Vanguard, draft.Adjutant })
            .OfType<GeneralId>().Distinct().ToArray();
        var lines = drafts.Select((draft, index) =>
        {
            var troop = _troops.First(t => t.Code == draft.TroopCode);
            var vanguard = _state.Generals.First(g => g.Id == draft.Vanguard).Name;
            var adjutant = draft.Adjutant is { } aid ? " + " + _state.Generals.First(g => g.Id == aid).Name : "";
            return $"{index + 1}. {troop.Name} {draft.Troops:N0}명 · {vanguard}{adjutant} · 금 {draft.Gold:N0}";
        }).ToList();
        ShowConfirm("일괄전투편성 확인",
            $"{string.Join("\n", lines)}\n\n확인 후 전투편성 목록에서 부대별 목표와 이동 모드를 지정하세요.{DutyReleaseNotice(ids)}",
            () =>
            {
                var currentReserved = ReservedDeployGenerals(-1, editingSupply: false);
                if (_advancing || ids.Any(id => _state.IsGeneralBusy(id)) || currentReserved.Overlaps(ids))
                {
                    ShowNotice("일괄편성 불가", "선택한 장수가 다른 업무 또는 출전 예약에 사용 중입니다.");
                    return;
                }

                _state = _state.ReleaseOfficerDuties(ids);
                foreach (var draft in drafts)
                {
                    var troop = _troops.First(t => t.Code == draft.TroopCode);
                    var vanguard = _state.Generals.First(g => g.Id == draft.Vanguard).Name;
                    var adjutant = draft.Adjutant is { } aid ? "+" + _state.Generals.First(g => g.Id == aid).Name : "";
                    var request = new DeployRequest(city, draft.TroopCode, draft.Troops,
                        draft.Vanguard, draft.Adjutant, UnitMode.Advance, Provisions: -1,
                        Gold: draft.Gold, DelayDays: _depDelayDays);
                    _pendingDeploys.Add((request, $"{troop.Name} {draft.Troops}({vanguard}{adjutant}) · 전진 · 군량 자동 · 금 {draft.Gold:N0} · {DelayLabel(_depDelayDays)}"));
                }
                _depSelectedUnit = -1;
                SelectCity(city);
                OpenDeployHub();
            });
    }

    private async void RunBatchDeployQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player && !c.IsPort
            && _state.Garrisons.Any(g => g.City == c.Id && g.Troops > 0));
        var beforeState = _state;
        var beforePending = _pendingDeploys.Count;
        OpenBatchDeployCompose(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>().ToList() ?? [];
        var options = _modalLayer?.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().ToList() ?? [];
        var troopOptions = options.Where(option => option.Name.ToString().StartsWith("BatchDeployTroopType", System.StringComparison.Ordinal)).ToList();
        var troopAmounts = _modalLayer?.FindChildren("BatchDeployTroops*", "SpinBox", true, false).OfType<SpinBox>().ToList() ?? [];
        var goldAmounts = _modalLayer?.FindChildren("BatchDeployGold*", "SpinBox", true, false).OfType<SpinBox>().ToList() ?? [];
        var removes = _modalLayer?.FindChildren("BatchDeployRemove*", "Button", true, false).OfType<Button>().ToList() ?? [];
        var delay = _modalLayer?.FindChildren("DeploymentDelaySelector", "OptionButton", true, false)
            .OfType<OptionButton>().FirstOrDefault();
        var delayValid = delay?.ItemCount == DeployService.MaxDeploymentDelayDays + 1;
        var unitCards = _modalLayer?.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
            .Count(panel => panel.HasMeta("deploy_unit_art")) ?? 0;
        var compactColumns = troopOptions.All(option => option.CustomMinimumSize.X <= 112 && !option.FitToLongestItem)
            && troopAmounts.All(amount => amount.CustomMinimumSize.X <= 92)
            && goldAmounts.All(gold => gold.CustomMinimumSize.X <= 92)
            && removes.All(remove => remove.CustomMinimumSize.X <= 56);
        var batchScroll = _modalLayer?.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>()
            .FirstOrDefault(scroll => scroll.GetMeta("batch_modal_scroll_disabled").AsBool());
        var noModalScroll = batchScroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
            && batchScroll.GetVScrollBar().MaxValue - batchScroll.GetVScrollBar().Page <= batchScroll.GetVScrollBar().MinValue + 0.5;
        var contentHeight = batchScroll?.GetMeta("batch_content_fitted_height").AsSingle() ?? 0f;
        var compactHeight = batchScroll is not null
            && batchScroll.CustomMinimumSize.Y >= contentHeight
            && batchScroll.CustomMinimumSize.Y - contentHeight <= 12f;
        var passed = ReferenceEquals(beforeState, _state)
            && beforePending == _pendingDeploys.Count
            && labels.Any(label => label.Text.Contains("일괄전투편성"))
            && options.Count > 0
            && troopAmounts.Count is > 0 and <= BatchDeployPlanner.MaxUnits
            && troopOptions.Count == troopAmounts.Count
            && goldAmounts.Count == troopAmounts.Count
            && removes.Count == troopAmounts.Count
            && delayValid
            && unitCards == troopAmounts.Count
            && compactColumns
            && noModalScroll
            && compactHeight;
        GD.Print($"[maptestbatchdeployqa] passed={passed} rows={troopAmounts.Count} troopOptions={troopOptions.Count} gold={goldAmounts.Count} remove={removes.Count} delay={delayValid} unitCards={unitCards} compact={compactColumns} noScroll={noModalScroll} compactHeight={compactHeight}:{batchScroll?.CustomMinimumSize.Y ?? 0:0}/{contentHeight:0} stateUnchanged={ReferenceEquals(beforeState, _state)} pending={beforePending}/{_pendingDeploys.Count}");
        CloseModal();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunDeployTargetReturnQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player && !c.IsPort);
        var garrison = _state.Garrisons.First(g => g.City == city.Id && g.Troops > 0);
        var general = (_state.Postings ?? []).First(p => p.Faction == Player && p.Location == city.Id).General;
        var request = new DeployRequest(city.Id, garrison.TroopCode, System.Math.Min(500, garrison.Troops),
            general, null, UnitMode.Advance, Provisions: -1);
        var pendingIndex = _pendingDeploys.Count;
        _pendingDeploys.Add((request, "목표 복귀 QA"));
        _depModalCity = city.Id;
        _depSelectedUnit = pendingIndex;

        BeginTargeting(pendingIndex);
        _targetWaypoints.Add(new HexCoord(city.Position.Q + 4, city.Position.R));
        RebuildTargetEdit();
        var directionControls = _targetEgressButtons.Count == 6
            && _targetEgressButtons.Values.Count(button => !button.Disabled) > 0;
        _targetWaypoints.Clear();
        ApplyTarget(city.Position, null);

        var labels = _modalLayer?.FindChildren("*", "Label", true, false)
            .OfType<Label>().Select(label => label.Text).ToList() ?? [];
        var targetSaved = _pendingDeploys[pendingIndex].Req.Target == city.Position;
        var egressSaved = _pendingDeploys[pendingIndex].Req.EgressDirection.HasValue
            && _pendingDeploys[pendingIndex].Req.EgressExit.HasValue;
        var hubReopened = _modalLayer is not null && labels.Any(text => text.Contains("출전 예약"));
        var selectionKept = _depSelectedUnit == pendingIndex;
        var commandPaletteHidden = !_cmdMenu.Visible;
        var passed = targetSaved && egressSaved && directionControls && hubReopened && selectionKept
            && commandPaletteHidden && !_depTargeting;
        GD.Print($"[maptestdeploytargetreturnqa] passed={passed} targetSaved={targetSaved} egressSaved={egressSaved} directionControls={directionControls} hubReopened={hubReopened} selectionKept={selectionKept} commandPaletteHidden={commandPaletteHidden} targeting={_depTargeting}");

        _pendingDeploys.RemoveAt(pendingIndex);
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
