namespace ecomm.api.Features.Media;

/// <summary>Outcome of a one-time resize backfill over images stored at full upload resolution
/// from before uploads were resized.</summary>
public sealed record BackfillResizeResult(
    int Candidates, int Resized, int AlreadySmall, int Failed, IReadOnlyList<string> FailedIds,
    long BytesBefore, long BytesAfter);
