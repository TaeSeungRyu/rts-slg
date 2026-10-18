namespace SanguoSLG.Core.Domain;

public sealed record DismissedGeneral(GeneralId General, FactionId FormerFaction, int ReturnDay);
