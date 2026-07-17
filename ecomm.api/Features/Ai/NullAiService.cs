using ecomm.api.Common.Exceptions;

namespace ecomm.api.Features.Ai;

/// <summary>No-op provider used when <c>Ai:Provider=None</c>. AI affordances stay hidden
/// (<see cref="Enabled"/> = false); a stray call errors clearly instead of doing anything.</summary>
public sealed class NullAiService : IAiService
{
    public bool Enabled => false;
    public long EstimateCostMicros(AiCompletion completion) => 0;
    public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        => throw new AppException("AI features are not enabled on this platform.", 503);
}
