namespace ecomm.api.Features.Media;

/// <summary>Outcome of a one-time watermark backfill over images stored before watermarking existed.</summary>
public sealed record BackfillWatermarksResult(
    int Candidates, int Watermarked, int AlreadyDone, int Failed, IReadOnlyList<string> FailedUrls);
