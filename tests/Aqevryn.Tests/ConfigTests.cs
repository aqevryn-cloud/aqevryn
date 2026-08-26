using Xunit;
using Aqevryn.Config;

namespace Aqevryn.Tests;

public class ConfigTests
{
    [Fact]
    public void LoadSettings_Defaults()
    {
        var settings = ConfigLoader.LoadSettings();
        Assert.Equal("development", settings.AppEnv);
        Assert.Equal("openai", settings.LlmProvider);
        Assert.False(settings.AutoPublish);
        Assert.Equal(90, settings.MinPublicationScore);
    }

    [Fact]
    public void LoadSettings_EnvironmentOverrides()
    {
        Environment.SetEnvironmentVariable("LLM_PROVIDER", "anthropic");
        Environment.SetEnvironmentVariable("LLM_MODEL", "claude-3-opus");
        Environment.SetEnvironmentVariable("AUTO_PUBLISH", "true");
        var settings = ConfigLoader.LoadSettings();
        Assert.Equal("anthropic", settings.LlmProvider);
        Assert.Equal("claude-3-opus", settings.LlmModel);
        Assert.True(settings.AutoPublish);
        Environment.SetEnvironmentVariable("LLM_PROVIDER", null);
        Environment.SetEnvironmentVariable("LLM_MODEL", null);
        Environment.SetEnvironmentVariable("AUTO_PUBLISH", null);
    }

    [Fact]
    public void LoadSources_MissingFile()
    {
        var sources = ConfigLoader.LoadSources("nonexistent.yaml");
        Assert.Empty(sources);
    }
}