using DiffToJsonLib.Reasoning;

namespace DiffToJsonLib.Tests.Reasoning;

public class ModelsDevProviderMapTests
{
    [Test]
    public async Task OpenAi_MapsToOpenAi()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("openai", out string? providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("openai");
    }

    [Test]
    public async Task Anthropic_MapsToAnthropic()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("anthropic", out string? providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("anthropic");
    }

    [Test]
    public async Task OpenRouter_MapsToOpenRouter()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("openrouter", out string? providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("openrouter");
    }

    [Test]
    public async Task Ollama_MapsToOllamaCloud()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("ollama", out string? providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("ollama-cloud");
    }

    [Test]
    public async Task OllamaCloud_MapsToOllamaCloud()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("ollama-cloud", out string? providerId);
        await Assert.That(found).IsTrue();
        await Assert.That(providerId).IsEqualTo("ollama-cloud");
    }

    [Test]
    public async Task UnknownProvider_ReturnsFalse()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId("unknown-provider", out string? providerId);
        await Assert.That(found).IsFalse();
        await Assert.That(providerId).IsNull();
    }

    [Test]
    public async Task NullProvider_ReturnsFalse()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId(null!, out string _);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task EmptyProvider_ReturnsFalse()
    {
        bool found = ModelsDevProviderMap.TryGetProviderId(string.Empty, out string _);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task CaseInsensitive_Lookup()
    {
        bool foundUpper = ModelsDevProviderMap.TryGetProviderId("OPENAI", out string? idUpper);
        bool foundLower = ModelsDevProviderMap.TryGetProviderId("openai", out string? idLower);
        await Assert.That(foundUpper).IsEqualTo(foundLower);
        await Assert.That(idUpper).IsEqualTo(idLower);
    }
}
