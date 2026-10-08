namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public interface IFieldAdvanceRunner
{
    bool UsesContinuousMovement => false;
    void SetCampaignState(GameState state) { }

    bool CanEnter(MovementDomain domain, HexCoord coord);

    AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7,
        IReadOnlyList<SiegeSite>? castles = null, IReadOnlySet<UnitId>? deployedToday = null,
        IReadOnlySet<UnitId>? constructionUnits = null,
        IReadOnlyList<FieldBuilding>? fieldBuildings = null,
        IReadOnlyList<FieldBuildingDefinition>? fieldDefinitions = null, int fieldDay = 0);
}
