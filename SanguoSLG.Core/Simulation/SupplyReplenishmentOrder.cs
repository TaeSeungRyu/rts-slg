namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record SupplyReplenishmentOrder(UnitId Source, UnitId Target, int Troops, int Gold, int Provisions);
