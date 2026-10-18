namespace SanguoSLG.Core.Simulation.Climate;

using SanguoSLG.Core.Spatial;

public sealed record ClimatePatch(int Id, ClimateKind Kind, HexCoord Origin,
    int StartDay, int EndDay);
