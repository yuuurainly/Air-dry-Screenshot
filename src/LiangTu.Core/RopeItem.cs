namespace LiangTu.Core;

public sealed record RopeItem(
    Guid Id,
    ImageAsset Image,
    int SortPosition,
    DateTimeOffset AddedAt);
