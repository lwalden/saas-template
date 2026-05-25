using Xunit;

namespace SaasTemplate.Api.Tests.Content;

/// <summary>
/// Regression tests for vercel.json — guards against reintroducing ignoreCommand,
/// which silently canceled every Vercel deployment (removed in PR #329).
/// </summary>
public class VercelConfigTests
{
    private static string SolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);
        return dir!;
    }

    [Fact]
    public void VercelJson_DoesNotContain_IgnoreCommand()
    {
        // Regression: ignoreCommand was removed in PR #329 because it caused Vercel
        // to silently cancel every deployment on shallow clones. Must not return.
        var json = File.ReadAllText(Path.Combine(SolutionRoot(), "vercel.json"));
        Assert.DoesNotContain("ignoreCommand", json);
    }
}
