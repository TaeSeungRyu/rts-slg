namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private void OpenMerchantTradeWindow(CityId cityId)
    {
        var visit = _state.Merchants.FirstOrDefault(item => item.City == cityId);
        var city = _state.Cities.FirstOrDefault(item => item.Id == cityId);
        if (visit is null || city is null) return;

        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(MerchantModalLayout.Width, viewport.X - MerchantModalLayout.ViewportMargin);
        var height = Mathf.Min(MerchantModalLayout.Height, viewport.Y - MerchantModalLayout.ViewportMargin);
        var contentWidth = ModalChrome.HasRequiredAssets
            ? width - ModalChrome.HorizontalChromeExtras : width;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var body = DeployScaffold(contentWidth, out var outerScroll, out var panel, useModalChrome: true);
        panel.CustomMinimumSize = new Vector2(width, height);
        panel.Size = panel.CustomMinimumSize;
        outerScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outerScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 8);
        var chrome = panel as ModalChrome;
        body.CustomMinimumSize = new Vector2(0f,
            Mathf.Max(200f, height - (chrome?.HeaderFooterHeight ?? 0f) - 24f));

        var header = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (chrome is not null) chrome.HeaderHost.AddChild(header);
        else body.AddChild(header);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(40f, 0f) });
        var title = MakeLabel($"◆ 상인 교역소 · {city.Name} ◆", MerchantModalLayout.TitleSize, GoldBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40f, 40f);
        close.Pressed += CloseModal;
        header.AddChild(close);

        var columns = new HBoxContainer
        {
            Name = "MerchantColumns",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", (int)MerchantModalLayout.ColumnGap);
        body.AddChild(columns);
        var innerWidth = width - 2f * ModalChrome.BodyHorizontalInset - 24f;
        var left = new PanelContainer
        {
            Name = "MerchantIdentity",
            CustomMinimumSize = new Vector2(innerWidth * MerchantModalLayout.LeftShare, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        left.AddThemeStyleboxOverride("panel", Frame(MerchantModalLayout.Panel, MerchantModalLayout.Gold, 1, 8, 10));
        columns.AddChild(left);
        var leftContent = new VBoxContainer();
        leftContent.AddThemeConstantOverride("separation", 8);
        left.AddChild(leftContent);
        var portrait = new PanelContainer();
        portrait.AddThemeStyleboxOverride("panel", Frame(MerchantModalLayout.Card, MerchantModalLayout.Gold, 2, 6, 3));
        leftContent.AddChild(portrait);
        var portraitNumber = (int)((uint)unchecked(visit.ArrivedDay * 31 + cityId.Value * 17) % 4) + 1;
        portrait.AddChild(MerchantImage($"상인{portraitNumber}.png",
            Mathf.Min(MerchantModalLayout.PortraitHeight, height * 0.36f), true));
        var merchantName = MakeLabel("떠돌이 상인", MerchantModalLayout.SectionSize, GoldBright);
        merchantName.HorizontalAlignment = HorizontalAlignment.Center;
        var nameplate = new PanelContainer();
        nameplate.AddThemeStyleboxOverride("panel", Frame(MerchantModalLayout.Card,
            MerchantModalLayout.Gold, 1, 6, 5));
        nameplate.AddChild(merchantName);
        leftContent.AddChild(nameplate);
        var greeting = MakeLabel("귀한 물건을 가져왔습니다.\n한번 둘러보시지요.", MerchantModalLayout.BodySize, Parchment);
        greeting.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        leftContent.AddChild(greeting);
        leftContent.AddChild(GoldRule());
        leftContent.AddChild(MakeLabel("보유 자원", MerchantModalLayout.BodySize, GoldBright));
        var goldAmount = MerchantResourceRow(leftContent, "money.png");
        var grainAmount = MerchantResourceRow(leftContent, "food.png");

        var divider = new ColorRect
        {
            Color = MerchantModalLayout.Gold,
            CustomMinimumSize = new Vector2(2f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        columns.AddChild(divider);

        var right = new VBoxContainer
        {
            Name = "MerchantTradeArea",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        right.AddThemeConstantOverride("separation", 10);
        columns.AddChild(right);
        var tabs = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabs.AddThemeConstantOverride("separation", 8);
        right.AddChild(tabs);
        var grainTabButton = MakeButton("군량 거래");
        var artifactTabButton = MakeButton("유물 구매");
        foreach (var button in new[] { grainTabButton, artifactTabButton })
        {
            button.CustomMinimumSize = new Vector2(0f, 44f);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.AddThemeFontSizeOverride("font_size", MerchantModalLayout.SectionSize);
            tabs.AddChild(button);
        }

        var grainTab = new HBoxContainer
        {
            Name = "MerchantGrainTab",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        grainTab.AddThemeConstantOverride("separation", (int)MerchantModalLayout.CardGap);
        right.AddChild(grainTab);
        var artifactTab = new VBoxContainer
        {
            Name = "MerchantArtifactTab",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        artifactTab.AddThemeConstantOverride("separation", 10);
        right.AddChild(artifactTab);

        Action refresh = () => { };
        void Commit(CommandResult result)
        {
            if (!result.Ok)
            {
                ShowNotice("상인 거래", result.Error ?? "거래할 수 없습니다.");
                return;
            }
            _state = result.State;
            Redraw("상인 거래를 마쳤습니다.");
            refresh();
        }

        var buyGrain = MerchantGrainCard(grainTab, "군량 구매", true, cityId, visit, Commit, () => refresh());
        var sellGrain = MerchantGrainCard(grainTab, "군량 판매", false, cityId, visit, Commit, () => refresh());

        artifactTab.AddChild(MakeLabel("구매한 유물은 단서·보물 보관함에서 장착할 수 있습니다.",
            MerchantModalLayout.BodySize, Parchment));
        var artifactScroll = new ScrollContainer
        {
            Name = "MerchantArtifactScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        artifactTab.AddChild(artifactScroll);
        var scrollInset = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scrollInset.AddThemeConstantOverride("margin_right", (int)MerchantModalLayout.ScrollContentGap);
        artifactScroll.AddChild(scrollInset);
        var products = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        products.AddThemeConstantOverride("h_separation", (int)MerchantModalLayout.CardGap);
        products.AddThemeConstantOverride("v_separation", (int)MerchantModalLayout.CardGap);
        scrollInset.AddChild(products);
        var productButtons = new List<(MerchantArtifact Artifact, Button Button)>();
        foreach (var code in visit.ArtifactCodes)
        {
            var artifact = MerchantCatalog.Find(code);
            if (artifact is null) continue;
            var product = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            product.AddThemeStyleboxOverride("panel", Frame(MerchantModalLayout.Card, MerchantModalLayout.Gold, 1, 7, 8));
            products.AddChild(product);
            var productContent = new VBoxContainer();
            productContent.AddThemeConstantOverride("separation", 6);
            product.AddChild(productContent);
            var heading = new HBoxContainer();
            heading.AddThemeConstantOverride("separation", 8);
            productContent.AddChild(heading);
            var index = MerchantCatalog.Artifacts.ToList().FindIndex(item => item.Code == code) + 1;
            heading.AddChild(MerchantImage($"{index:00}_{artifact.Name}.png", 64f));
            var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            heading.AddChild(details);
            details.AddChild(MakeLabel(artifact.Name, MerchantModalLayout.BodySize, GoldBright));
            var effect = artifact.Effect switch
            {
                "might_percent" => $"무력 +{artifact.Value}%",
                "movement_percent" => $"부대 이동 +{artifact.Value}%",
                _ => $"지력 +{artifact.Value}%",
            };
            if (artifact.DefensePercent > 0) effect += $" · 방어 +{artifact.DefensePercent}%";
            var effectLabel = MakeLabel(effect, 12, MerchantModalLayout.Green);
            effectLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            details.AddChild(effectLabel);
            var purchase = MakeButton($"금 {artifact.GoldCost:N0} · 구매", accent: true);
            purchase.Pressed += () => ConfirmMerchantAction(
                $"{artifact.Name}을 금 {artifact.GoldCost:N0}에 구매하시겠습니까?",
                () => Commit(MerchantService.BuyArtifact(_state, cityId, Player, artifact.Code)));
            productContent.AddChild(purchase);
            productButtons.Add((artifact, purchase));
        }

        var footer = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddChild(footer);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var dismiss = MakeButton("상인 떠나 보내기");
        dismiss.CustomMinimumSize = new Vector2(MerchantModalLayout.FooterButtonWidth, 38f);
        dismiss.Disabled = city.Owner != Player;
        dismiss.Pressed += () => ConfirmMerchantAction("상인을 떠나 보내시겠습니까?", () =>
        {
            _state = MerchantService.Dismiss(_state, cityId);
            CloseModal();
            Redraw("상인이 떠났습니다.");
        });
        footer.AddChild(dismiss);

        void SelectTab(bool showGrain)
        {
            grainTab.Visible = showGrain;
            artifactTab.Visible = !showGrain;
            StyleMerchantTab(grainTabButton, showGrain);
            StyleMerchantTab(artifactTabButton, !showGrain);
        }
        grainTabButton.Pressed += () => SelectTab(true);
        artifactTabButton.Pressed += () => SelectTab(false);
        SelectTab(true);

        refresh = () =>
        {
            var current = _state.Cities.First(item => item.Id == cityId);
            goldAmount.Text = $"금 {current.Gold:N0}";
            grainAmount.Text = $"군량 {current.Provisions:N0}";
            buyGrain(current);
            sellGrain(current);
            foreach (var (artifact, button) in productButtons)
            {
                button.Disabled = current.Owner != Player || current.Gold < artifact.GoldCost;
                button.Text = current.Gold < artifact.GoldCost
                    ? $"금 {artifact.GoldCost:N0} · 금 부족" : $"금 {artifact.GoldCost:N0} · 구매";
            }
        };
        refresh();
        CenterAndDrag(panel, header, width, height, body);
    }

    private Action<City> MerchantGrainCard(HBoxContainer parent, string heading,
        bool buying, CityId cityId, MerchantVisit visit, Action<CommandResult> commit, Action changed)
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", Frame(MerchantModalLayout.Card, MerchantModalLayout.Gold, 1, 7, 10));
        parent.AddChild(card);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        card.AddChild(content);
        var headingRow = new HBoxContainer();
        headingRow.AddThemeConstantOverride("separation", 8);
        headingRow.AddChild(MerchantImage("food.png", 60f, fromExisting: true));
        var headingText = new VBoxContainer();
        headingRow.AddChild(headingText);
        headingText.AddChild(MakeLabel(heading, MerchantModalLayout.SectionSize, GoldBright));
        var price = MakeLabel("", MerchantModalLayout.BodySize, Parchment);
        headingText.AddChild(price);
        content.AddChild(headingRow);
        content.AddChild(GoldRule());
        var amountRow = new HBoxContainer();
        amountRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(amountRow);
        var minus = MakeButton("−");
        minus.CustomMinimumSize = new Vector2(36f, 36f);
        amountRow.AddChild(minus);
        var amount = new SpinBox
        {
            MinValue = 100,
            MaxValue = 100000,
            Step = 100,
            Value = 100,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(100f, 36f),
        };
        amountRow.AddChild(amount);
        var plus = MakeButton("+");
        plus.CustomMinimumSize = new Vector2(36f, 36f);
        amountRow.AddChild(plus);
        minus.Pressed += () => amount.Value = Mathf.Max(100, (float)amount.Value - 100f);
        plus.Pressed += () => amount.Value = Mathf.Min(100000, (float)amount.Value + 100f);
        amount.ValueChanged += _ => changed();
        var total = MakeLabel("", MerchantModalLayout.BodySize, GoldBright);
        content.AddChild(total);
        content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var action = MakeButton(heading, accent: true);
        action.CustomMinimumSize = new Vector2(0f, 42f);
        content.AddChild(action);
        action.Pressed += () =>
        {
            var quantity = (int)amount.Value;
            ConfirmMerchantAction($"군량 {quantity:N0}을 {(buying ? "구매" : "판매")}하시겠습니까?", () =>
            {
                var city = _state.Cities.First(item => item.Id == cityId);
                var basePrice = _commander.MarketUnitPricePer100(_state, city, MarketResource.Grain);
                commit(buying
                    ? MerchantService.BuyGrain(_state, cityId, Player, quantity, basePrice)
                    : MerchantService.SellGrain(_state, cityId, Player, quantity, basePrice));
            });
        };
        void Refresh(City city)
        {
            var basePrice = _commander.MarketUnitPricePer100(_state, city, MarketResource.Grain);
            var percent = buying ? visit.BuyGrainPercent : visit.SellGrainPercent;
            var quantity = (int)amount.Value;
            var value = buying
                ? ((long)basePrice * percent * quantity + 9999) / 10000
                : (long)basePrice * percent * quantity / 10000;
            price.Text = $"100군량당 금 {(buying ? (basePrice * percent + 99) / 100 : basePrice * percent / 100):N0}";
            total.Text = $"{(buying ? "구매 비용" : "판매 수익")}  금 {value:N0}";
            action.Disabled = city.Owner != Player || (buying ? city.Gold < value : city.Provisions < quantity);
        }
        return Refresh;
    }

    private Label MerchantResourceRow(VBoxContainer parent, string icon)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(MerchantImage(icon, 28f, fromExisting: true));
        var value = MakeLabel("", MerchantModalLayout.SectionSize, GoldBright);
        value.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(value);
        parent.AddChild(row);
        return value;
    }

    private static TextureRect MerchantImage(string filename, float size, bool portrait = false,
        bool fromExisting = false)
    {
        var path = fromExisting ? $"res://assets/ui/game/deploy_command/{filename}"
            : MerchantModalLayout.AssetRoot + filename;
        return new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = portrait ? TextureRect.StretchModeEnum.KeepAspectCovered
                : TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsHorizontal = portrait ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = portrait,
        };
    }

    private void StyleMerchantTab(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride("normal", Frame(selected ? MerchantModalLayout.Selected
            : MerchantModalLayout.Panel, selected ? GoldBright : MerchantModalLayout.Gold, 2, 7, 8));
        button.AddThemeStyleboxOverride("hover", Frame(selected ? MerchantModalLayout.Selected
            : MerchantModalLayout.Hover, GoldBright, 2, 7, 8));
        button.AddThemeStyleboxOverride("pressed", Frame(MerchantModalLayout.Selected, GoldBright, 2, 7, 8));
        button.AddThemeColorOverride("font_color", selected ? GoldBright : Parchment);
    }
}
