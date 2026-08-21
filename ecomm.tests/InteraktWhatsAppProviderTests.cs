using System.Net;
using System.Text.Json;
using ecomm.api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class InteraktWhatsAppProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public HttpResponseMessage Response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result":true,"message":"Message created successfully","id":"msg-abc-123"}"""),
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return Response;
        }
    }

    private static (InteraktWhatsAppProvider provider, FakeHandler handler) NewProvider(WhatsAppOptions? opts = null)
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler);
        var provider = new InteraktWhatsAppProvider(http, Options.Create(opts ?? new WhatsAppOptions
        {
            Provider = "Interakt", ApiKey = "test-basic-auth-key",
        }), NullLogger<InteraktWhatsAppProvider>.Instance);
        return (provider, handler);
    }

    [Fact]
    public async Task Template_send_posts_split_country_code_and_number_with_basic_auth_header()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendTemplateMessageAsync("9876543210", "order_shipped", ["Sam", "ORD-1"]);

        Assert.True(result.Success);
        Assert.Equal("msg-abc-123", result.MessageId);
        Assert.Equal("api.interakt.ai", handler.LastRequest!.RequestUri!.Host);
        Assert.Equal("Basic test-basic-auth-key", handler.LastRequest.Headers.GetValues("Authorization").Single());
        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("+91", json.GetProperty("countryCode").GetString());
        Assert.Equal("9876543210", json.GetProperty("phoneNumber").GetString());
        Assert.Contains("order_shipped", handler.LastBody);
        Assert.Contains("Sam", handler.LastBody);
    }

    [Fact]
    public async Task Twelve_digit_number_with_91_prefix_is_split_into_country_code_and_bare_number()
    {
        var (provider, handler) = NewProvider();

        await provider.SendTemplateMessageAsync("919876543210", "t", []);

        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("+91", json.GetProperty("countryCode").GetString());
        Assert.Equal("9876543210", json.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task Unparseable_phone_fails_without_making_a_request()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendTemplateMessageAsync("123", "t", []);

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
    public async Task Result_false_in_a_200_response_is_reported_as_failure()
    {
        var (provider, handler) = NewProvider();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result":false,"message":"template not found"}"""),
        };

        var result = await provider.SendTemplateMessageAsync("9876543210", "t", []);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Session_message_uses_type_Text_with_a_data_message_field()
    {
        var (provider, handler) = NewProvider();

        var result = await provider.SendSessionMessageAsync("9876543210", "Hi there");

        Assert.True(result.Success);
        Assert.Contains("\"type\":\"Text\"", handler.LastBody);
        Assert.Contains("Hi there", handler.LastBody);
    }
}
