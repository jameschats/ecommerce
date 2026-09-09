using System.Net;
using ecomm.api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Exercises <see cref="GupshupWhatsAppProvider"/> against its real contract: Gupshup's
/// GatewayAPI/rest line (mediaapi.smsgupshup.com), form-urlencoded, Bearer secret token,
/// userid/send_to fields, and a `{"response":{...,"status":"success"}}` response envelope.
/// Rewritten 2026-09-09 — the previous version asserted the earlier api.gupshup.io/sm/api/v1
/// contract (an `apikey` header, `destination=`, a bare `{"status":"submitted"}` response) that
/// the provider stopped using back on 2026-08-28; those stale assertions were failing against
/// live code for two weeks before anyone noticed, since the mismatch never affected a real send
/// (no Meta-approved template exists yet to actually verify against).
/// </summary>
public class GupshupWhatsAppProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public HttpResponseMessage Response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"response":{"id":"abc-123","phone":"919876543210","details":"submitted","status":"success"}}"""),
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return Response;
        }
    }

    private static (GupshupWhatsAppProvider provider, FakeHandler handler) NewProvider(WhatsAppOptions? opts = null)
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler);
        var provider = new GupshupWhatsAppProvider(http, Options.Create(opts ?? new WhatsAppOptions
        {
            Provider = "Gupshup", UserId = "2000270417", ApiKey = "test-secret-token",
        }), NullLogger<GupshupWhatsAppProvider>.Instance);
        return (provider, handler);
    }

    [Fact]
    public async Task Template_send_posts_userid_send_to_and_template_fields_with_bearer_auth()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendTemplateMessageAsync("9876543210", "tmpl-guid-1", ["Sam", "ORD-1"]);

        Assert.True(result.Success);
        Assert.Equal("abc-123", result.MessageId);
        Assert.Equal("https://mediaapi.smsgupshup.com/GatewayAPI/rest", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer test-secret-token", handler.LastRequest.Headers.GetValues("Authorization").Single());
        Assert.Contains("method=SendMessage", handler.LastBody);
        Assert.Contains("userid=2000270417", handler.LastBody);
        Assert.Contains("send_to=919876543210", handler.LastBody);
        Assert.Contains("isHSM=true", handler.LastBody);
        Assert.Contains("isTemplate=true", handler.LastBody);
        Assert.Contains("whatsAppTemplateId=tmpl-guid-1", handler.LastBody);
        Assert.Contains("var1=Sam", handler.LastBody);
        Assert.Contains("var2=ORD-1", handler.LastBody);
    }

    [Fact]
    public async Task Ten_digit_number_is_normalized_with_the_India_country_code()
    {
        var (provider, handler) = NewProvider();

        await provider.SendTemplateMessageAsync("9876543210", "t", []);

        Assert.Contains("send_to=919876543210", handler.LastBody);
    }

    [Fact]
    public async Task Unparseable_phone_fails_without_making_a_request()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendTemplateMessageAsync("abc", "t", []);

        Assert.False(result.Success);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Non_success_http_status_is_reported_as_failure()
    {
        var (provider, handler) = NewProvider();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad key") };

        var result = await provider.SendTemplateMessageAsync("9876543210", "t", []);

        Assert.False(result.Success);
        Assert.Contains("401", result.Error);
    }

    [Fact]
    public async Task Non_success_status_in_a_200_response_is_reported_as_failure()
    {
        var (provider, handler) = NewProvider();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"response":{"id":"","details":"invalid template","status":"failed"}}"""),
        };

        var result = await provider.SendTemplateMessageAsync("9876543210", "t", []);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Session_message_posts_msg_field_with_no_template_flags()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendSessionMessageAsync("9876543210", "Hi there");

        Assert.True(result.Success);
        Assert.Equal("https://mediaapi.smsgupshup.com/GatewayAPI/rest", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("send_to=919876543210", handler.LastBody);
        Assert.Contains("msg=Hi+there", handler.LastBody);
        Assert.DoesNotContain("isTemplate", handler.LastBody);
        Assert.DoesNotContain("whatsAppTemplateId", handler.LastBody);
    }
}
