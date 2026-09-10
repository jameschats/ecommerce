namespace ecomm.api.Features.Media;

/// <summary>Outcome of restoring gallery photos from their pre-processing backup.</summary>
public sealed record RestoreOriginalsResult(
    int Candidates, int Restored, int NoBackupFound, int Failed, IReadOnlyList<string> FailedIds);
