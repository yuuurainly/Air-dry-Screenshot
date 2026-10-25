namespace LiangTu.Core;

public sealed record PinInstanceState(
    Guid Id,
    Guid? ImageId,
    double X,
    double Y,
    double Width,
    double Height,
    double Opacity,
    bool IsTopmost,
    DateTimeOffset CreatedAt);
