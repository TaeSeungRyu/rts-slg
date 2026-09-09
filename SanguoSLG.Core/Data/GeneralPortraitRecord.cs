namespace SanguoSLG.Core.Data;

public sealed record GeneralPortraitRecord(
    int GeneralId,
    string PortraitPath,
    double FaceCenterX,
    double FaceCenterY,
    double FaceZoom);

