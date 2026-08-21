using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.Support;

/// <summary>
/// Shopper-facing conversations. Starting one is allowed anonymously (rate-limited); reading and
/// replying need either a signed-in shopper who owns the thread, or the signed reply token from
/// their email link.
/// </summary>
[ApiController]
[Route("api/conversations")]
public sealed class ConversationController(IShopperConversationService convos, IChatbotService chatbot) : ControllerBase
{
    private long? ShopperId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> Start(StartConversationRequest request, CancellationToken ct)
        => Ok(ApiResponse<ConversationThreadDto>.Ok(await convos.StartAsync(request, ShopperId, ct), "Message sent."));

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ConversationDto>>.Ok(await convos.MineAsync(ShopperId ?? 0, ct)));

    [HttpGet("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<ConversationThreadDto>.Ok(await convos.GetForShopperAsync(id, ShopperId ?? 0, ct)));

    [HttpPost("{id:long}/messages")]
    [Authorize]
    public async Task<IActionResult> Reply(long id, ReplyRequest request, CancellationToken ct)
    {
        await convos.ReplyAsShopperAsync(id, request.Body, ShopperId, null, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Reply sent."));
    }

    /// <summary>Send a chat message to the AI assistant (v4 Phase 2). Authenticated only — the bot's
    /// order-lookup grounding needs a real customer identity, same reason anonymous threads can't
    /// use this yet. Falls back to a human on escalation; the widget shows <c>escalated</c> to know
    /// when to stop expecting bot replies and just wait for a person.</summary>
    [HttpPost("{id:long}/chat")]
    [Authorize]
    public async Task<IActionResult> Chat(long id, ReplyRequest request, CancellationToken ct)
        => Ok(ApiResponse<ChatbotReplyDto>.Ok(await chatbot.HandleShopperMessageAsync(id, request.Body, ShopperId ?? 0, ct)));

    /// <summary>Open a thread from the emailed link — no sign-in required.</summary>
    [HttpGet("thread/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> ByToken(string token, CancellationToken ct)
        => Ok(ApiResponse<ConversationThreadDto>.Ok(await convos.GetByTokenAsync(token, ct)));

    [HttpPost("thread/{token}/messages")]
    [AllowAnonymous]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> ReplyByToken(string token, ReplyRequest request, CancellationToken ct)
    {
        var thread = await convos.GetByTokenAsync(token, ct);
        await convos.ReplyAsShopperAsync(thread.Conversation.Id, request.Body, null, token, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Reply sent."));
    }
}

/// <summary>Merchant inbox for shopper conversations.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/inbox")]
public sealed class ConversationAdminController(IShopperConversationService convos) : ControllerBase
{
    private long MerchantUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ConversationDto>>.Ok(await convos.InboxAsync(status, page, pageSize, ct)));

    [HttpGet("open-count")]
    public async Task<IActionResult> OpenCount(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { count = await convos.OpenCountAsync(ct) }));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<ConversationThreadDto>.Ok(await convos.GetForMerchantAsync(id, ct)));

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> Reply(long id, ReplyRequest request, CancellationToken ct)
    {
        await convos.ReplyAsMerchantAsync(id, request.Body, MerchantUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Reply sent."));
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        await convos.SetStatusAsync(id, request.Status, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Conversation updated."));
    }

    /// <summary>
    /// Suggest a reply for the merchant to edit. Never sends — the merchant always ships the words.
    /// </summary>
    [HttpPost("{id:long}/draft")]
    public async Task<IActionResult> Draft(long id, [FromServices] ISupportDraftService drafts, CancellationToken ct)
        => Ok(ApiResponse<SupportDraftDto>.Ok(await drafts.DraftReplyAsync(id, ct)));
}
