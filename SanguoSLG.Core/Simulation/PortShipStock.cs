namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record PortShipStock(CityId City, string ShipCode, int Count);
