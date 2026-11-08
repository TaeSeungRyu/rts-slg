using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private const float SiteDragThreshold = 7f;
    private MapEditorSite? _sitePointerCandidate;
    private Vector2I _sitePointerTile;
    private Vector2 _sitePointerDown;
    private bool _siteDragging;
    private MapEditorSite? _siteDragDestination;
    private bool _siteDragDestinationValid;
    private string _siteDragReason = "";
    private Node3D? _siteDragPreview;

    private void BeginSitePointer(MapEditorSite site, Vector2I clickedTile, Vector2 mapPosition)
    {
        CancelSiteDrag();
        _sitePointerCandidate = site;
        _sitePointerTile = clickedTile;
        _sitePointerDown = mapPosition;
        _siteDragging = false;
        _tileInfo.Text = $"{site.Name} 선택 · 드래그하면 시설물을 이동합니다.";
    }

    private void UpdateSitePointer(Vector2 scenePosition)
    {
        if (_sitePointerCandidate is not { } site) return;
        if (!MapViewportContains(scenePosition))
        {
            ClearSiteDragPreview();
            _siteDragDestination = null;
            _siteDragDestinationValid = false;
            _siteDragReason = "지도 범위를 벗어났습니다.";
            _tileInfo.Text = $"이동 불가 · {_siteDragReason}";
            return;
        }
        var mapPosition = ToMapViewportPosition(scenePosition);
        if (!_siteDragging && mapPosition.DistanceTo(_sitePointerDown) < SiteDragThreshold) return;
        _siteDragging = true;
        if (TileAtMapPosition(mapPosition) is not { } target)
        {
            ClearSiteDragPreview();
            _siteDragDestination = null;
            return;
        }
        var anchor = new Vector2I(site.Column, site.Row) + (target - _sitePointerTile);
        var candidate = site with { Column = anchor.X, Row = anchor.Y };
        _siteDragDestinationValid = ValidateSiteMove(site, candidate, out _siteDragReason);
        _siteDragDestination = candidate;
        ShowSiteDragPreview(candidate, _siteDragDestinationValid);
        _tileInfo.Text = _siteDragDestinationValid
            ? $"이동 가능 · {site.Name} → 좌표 {anchor.X}, {anchor.Y} · 놓아서 확정"
            : $"이동 불가 · {_siteDragReason}";
    }

    private void FinishSitePointer(Vector2 scenePosition)
    {
        var site = _sitePointerCandidate;
        var wasDragging = _siteDragging;
        var destination = _siteDragDestination;
        var valid = _siteDragDestinationValid;
        var reason = _siteDragReason;
        var clickedMapPosition = MapViewportContains(scenePosition)
            ? ToMapViewportPosition(scenePosition) : _sitePointerDown;
        CancelSiteDrag();
        if (site is null) return;
        if (!wasDragging)
        {
            Select3DTile(clickedMapPosition, scenePosition);
            return;
        }
        if (!valid || destination is null)
        {
            _tileInfo.Text = $"{site.Name} 이동 취소 · {reason}";
            return;
        }
        if (destination.Column == site.Column && destination.Row == site.Row)
        {
            _tileInfo.Text = $"{site.Name} · 원래 위치를 유지합니다.";
            return;
        }
        var before = Draft();
        var index = _sites.FindIndex(current => current.Id == site.Id);
        if (index < 0) return;
        _sites[index] = destination;
        RecordChange(before);
        RefreshSiteVisuals();
        _selectedTile = new Vector2I(destination.Column, destination.Row);
        ShowSelectedTile(_selectedTile.Value);
        _tileInfo.Text = $"{site.Name} 이동 완료 · 좌표 {destination.Column}, {destination.Row} · ID {site.Id} 유지";
    }

    private bool ValidateSiteMove(MapEditorSite original, MapEditorSite candidate, out string reason)
    {
        if (!new[] { "city", "port", "gate", "ruin", "facility", "landmark" }.Contains(original.Kind))
        {
            reason = "이 시설물 유형은 안전한 이동 규칙이 없습니다.";
            return false;
        }
        if (!Footprint(candidate).All(Inside))
        {
            reason = "시설물 영역이 지도 밖으로 나갑니다.";
            return false;
        }
        if (!CanPlace(candidate))
        {
            reason = "지형 조건을 만족하지 않거나 다른 시설물과 겹칩니다.";
            return false;
        }
        if (candidate.Kind == "port" && !PortFacesWater(candidate))
        {
            reason = "항구 입구 방향에 접근 가능한 연안 또는 해역이 없습니다.";
            return false;
        }
        if (candidate.Kind == "facility")
        {
            var parent = _sites.FirstOrDefault(site => site.Kind == "city" && site.Id == candidate.ParentCityId);
            if (parent is null || !Footprint(candidate).Any(cell => Footprint(parent)
                    .Any(cityCell => Axial(cell).Distance(Axial(cityCell)) <= 2)))
            {
                reason = "생산 시설은 연결된 성의 경계에서 2칸 이내여야 합니다.";
                return false;
            }
        }
        if (candidate.Kind == "city")
        {
            var cityCells = Footprint(candidate).ToArray();
            var detached = _sites.Where(site => site.Kind == "facility" && site.ParentCityId == candidate.Id)
                .Any(facility => !Footprint(facility).Any(cell => cityCells
                    .Any(cityCell => Axial(cell).Distance(Axial(cityCell)) <= 2)));
            if (detached)
            {
                reason = "연결된 생산 시설이 성의 관리 범위를 벗어납니다.";
                return false;
            }
        }
        reason = "";
        return true;
    }

    private void ShowSiteDragPreview(MapEditorSite candidate, bool valid)
    {
        ClearSiteDragPreview();
        _siteDragPreview = new Node3D { Name = "SiteDragPreview" };
        _visibleTerrain.AddChild(_siteDragPreview);
        var color = valid ? new Color(0.93f, 0.75f, 0.28f, 0.72f) : new Color(0.92f, 0.20f, 0.14f, 0.76f);
        foreach (var cell in Footprint(candidate).Where(Inside))
        {
            var marker = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.58f,
                    BottomRadius = 0.58f,
                    Height = 0.018f,
                    RadialSegments = 6,
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    NoDepthTest = true,
                },
                Position = _mapView.HexToWorld(Axial(cell)) + new Vector3(0f, _mapView.TileTopY + 0.12f, 0f),
            };
            _siteDragPreview.AddChild(marker);
        }
    }

    private void ClearSiteDragPreview()
    {
        _siteDragPreview?.QueueFree();
        _siteDragPreview = null;
    }

    private void CancelSiteDrag()
    {
        ClearSiteDragPreview();
        _sitePointerCandidate = null;
        _siteDragging = false;
        _siteDragDestination = null;
        _siteDragDestinationValid = false;
        _siteDragReason = "";
    }

    public override void _Input(InputEvent input)
    {
        if (_sitePointerCandidate is null) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
        {
            FinishSitePointer(release.Position);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            var name = _sitePointerCandidate.Name;
            CancelSiteDrag();
            _tileInfo.Text = $"{name} 이동을 취소했습니다.";
            GetViewport().SetInputAsHandled();
        }
    }
}
