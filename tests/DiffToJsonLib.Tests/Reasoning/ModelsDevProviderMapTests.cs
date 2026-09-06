using DiffToJsonLib.Reasoning;

namespace DiffToJsonLib.Tests.Reasoning;

public class ModelsDevProviderMapTests
{
    [Test]
    public async Task OpenAi_MapsToOpenAi()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("openai", out var providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("openai");
    }

    [Test]
    public async Task Anthropic_MapsToAnthropic()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("anthropic", out var providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("anthropic");
    }

    [Test]
    public async Task OpenRouter_MapsToOpenRouter()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("openrouter", out var providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("openrouter");
    }

    [Test]
    public async Task Ollama_MapsToOllamaCloud()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("ollama", out var providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("ollama-cloud");
    }

    [Test]
    public async Task OllamaCloud_MapsToOllamaCloud()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("ollama-cloud", out var providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("ollama-cloud");
    }

    [Test]
    public async Task UnknownProvider_ReturnsFalse()
    {
        var found = ModelsDevProviderMap.TryGetProviderId("unknown-provider", out var providerId);
        await Assert.That(found).IsFalse();
        await Assert.That(providerId).IsNull();
    }

    [Test]
    public async Task NullProvider_ReturnsFalse()
    {
        var found = ModelsDevProviderMap.TryGetProviderId(null!, out var providerId);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task EmptyProvider_ReturnsFalse()
    {
        var found = ModelsDevProviderMap.TryGetProviderId(string.Empty, out var providerId);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task CaseInsensitive_Lookup()
    {
        var foundUpper = ModelsDevProviderMap.TryGetProviderId("OPENAI", out var idUpper);
        var foundLower = ModelsDevProviderMap.TryGetProviderId("openai", out var idLower);
        await Assert.That(foundUpper).IsEqualTo(foundLower);
        await Assert.That(idUpper).IsEqualTo(idLower);
    }
}
