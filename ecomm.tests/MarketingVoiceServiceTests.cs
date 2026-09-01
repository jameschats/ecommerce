using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Media;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class MarketingVoiceServiceTests
{
    private sealed class FakeTts(bool enabled, byte[]? audio) : ITextToSpeech
    {
        public bool Enabled => enabled;
        public string? LastLanguage;
        public Task<TtsAudio?> SynthesizeAsync(string text, string languageCode, string? speaker, CancellationToken ct = default)
        {
            LastLanguage = languageCode;
            return Task.FromResult(audio is null ? null : new TtsAudio(audio, "audio/mpeg", "mp3"));
        }
    }

    private sealed class FakeMedia : IMediaStorage
    {
        public string? SavedName;
        public Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, CancellationToken ct = default)
        { SavedName = originalName; return Task.FromResult(new StoredFile($"https://cdn/{originalName}", originalName, data.Length)); }
        public Task<bool> SaveVariantAsync(string u, string s, Stream d, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    }

    [Fact]
    public void Options_reports_enabled_state_and_indian_languages()
    {
        var opt = new MarketingVoiceService(new FakeTts(true, null), new FakeMedia()).Options();
        Assert.True(opt.Enabled);
        Assert.Contains(opt.Languages, l => l.Code == "ta-IN" && l.Name == "Tamil");
        Assert.Contains(opt.Speakers, s => s.Code == "");   // Auto option present
    }

    [Fact]
    public async Task Preview_when_disabled_returns_409()
    {
        var svc = new MarketingVoiceService(new FakeTts(false, null), new FakeMedia());
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.PreviewAsync(new VoicePreviewRequest("hi", "hi-IN", null)));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Preview_synthesises_stores_mp3_and_returns_url()
    {
        var media = new FakeMedia();
        var svc = new MarketingVoiceService(new FakeTts(true, new byte[] { 1, 2, 3 }), media);

        var res = await svc.PreviewAsync(new VoicePreviewRequest("Namaste", "hi-IN", "anushka"));

        Assert.Contains("voiceover.mp3", res.AudioUrl);
        Assert.Equal("voiceover.mp3", media.SavedName);
    }

    [Fact]
    public async Task Preview_falls_back_to_english_for_an_unknown_language()
    {
        var tts = new FakeTts(true, new byte[] { 1 });
        var svc = new MarketingVoiceService(tts, new FakeMedia());
        await svc.PreviewAsync(new VoicePreviewRequest("hi", "xx-YY", null));
        Assert.Equal("en-IN", tts.LastLanguage);
    }

    [Fact]
    public async Task Preview_when_synthesis_fails_returns_502()
    {
        var svc = new MarketingVoiceService(new FakeTts(true, null), new FakeMedia());
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.PreviewAsync(new VoicePreviewRequest("hi", "hi-IN", null)));
        Assert.Equal(502, ex.StatusCode);
    }
}
