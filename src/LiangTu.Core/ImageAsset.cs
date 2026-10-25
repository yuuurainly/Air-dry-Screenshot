namespace LiangTu.Core;

public sealed record ImageAsset(
    Guid Id,
    string FilePath,
    string ContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    int PixelWidth,
    int PixelHeight,
    ImageSourceType SourceType);
