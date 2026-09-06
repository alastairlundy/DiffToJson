using DiffToJsonLib.Reasoning;

namespace DiffToJsonLib.Tests.Reasoning;

public class ModelIdNormalizerTests
{
    [Test]
    public async Task StripCloudSuffix_RemovesColonCloud()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:cloud", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripCloudSuffix_CaseInsensitive()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:Cloud", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripQuantizationSuffix_Q4Km()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:q4_k_m", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripQuantizationSuffix_Q80()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:q8_0", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripQuantizationSuffix_Mlx()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:mlx", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripQuantizationSuffix_Fp16()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:fp16", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task StripQuantizationSuffix_Bf16()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:bf16", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task SizeSuffixTranslatesToDash_WhenVariantExists()
    {
        var variants = new HashSet<string> { "qwen3.5-9b" };
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:9b", variants);
        await Assert.That(result).IsEqualTo("qwen3.5-9b");
    }

    [Test]
    public async Task SizeSuffixTranslatesToDash_40b()
    {
        var variants = new HashSet<string> { "qwen3.5-40b" };
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:40b", variants);
        await Assert.That(result).IsEqualTo("qwen3.5-40b");
    }

    [Test]
    public async Task SizeSuffixReturnsNull_WhenVariantDoesNotExist()
    {
        var variants = new HashSet<string> { "qwen3.5-9b" };
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:4b", variants);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task NoColon_ReturnsModelIdUnchanged()
    {
        var result = ModelIdNormalizer.ResolveModelId("gpt-4o", null);
        await Assert.That(result).IsEqualTo("gpt-4o");
    }

    [Test]
    public async Task NullInput_ReturnsNull()
    {
        var result = ModelIdNormalizer.ResolveModelId(null!, null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task EmptyInput_ReturnsNull()
    {
        var result = ModelIdNormalizer.ResolveModelId(string.Empty, null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CloudThenQuantization_BothStripped()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:cloud:q4_k_m", null);
        await Assert.That(result).IsEqualTo("qwen3.5");
    }

    [Test]
    public async Task UnknownQuantizationSuffix_KeptAsIs()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:custom", null);
        await Assert.That(result).IsEqualTo("qwen3.5:custom");
    }

    [Test]
    public async Task NullVariants_SizeSuffixKeptAsIs()
    {
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:9b", null);
        await Assert.That(result).IsEqualTo("qwen3.5:9b");
    }

    [Test]
    public async Task EmptyVariants_SizeSuffixKeptAsIs()
    {
        var variants = new HashSet<string>();
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:9b", variants);
        await Assert.That(result).IsEqualTo("qwen3.5:9b");
    }

    [Test]
    public async Task QuantizationThenSizeSuffix_QuantizationStripped_SizeTranslated()
    {
        var variants = new HashSet<string> { "qwen3.5-9b" };
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:9b:q4_k_m", variants);
        await Assert.That(result).IsEqualTo("qwen3.5-9b");
    }

    [Test]
    public async Task CloudThenSizeSuffix_SizeTranslated()
    {
        var variants = new HashSet<string> { "qwen3.5-9b" };
        var result = ModelIdNormalizer.ResolveModelId("qwen3.5:cloud:9b", variants);
        await Assert.That(result).IsEqualTo("qwen3.5-9b");
    }

    [Test]
    public async Task AvailableVariantsMatching_ReturnsModelId()
    {
        var variants = new HashSet<string> { "gpt-4o", "gpt-4o-mini" };
        var result = ModelIdNormalizer.ResolveModelId("gpt-4o", variants);
        await Assert.That(result).IsEqualTo("gpt-4o");
    }

    [Test]
    public async Task AvailableVariantsNotMatching_ReturnsNull()
    {
        var variants = new HashSet<string> { "gpt-4o", "gpt-4o-mini" };
        var result = ModelIdNormalizer.ResolveModelId("gpt-5", variants);
        await Assert.That(result).IsNull();
    }
}
