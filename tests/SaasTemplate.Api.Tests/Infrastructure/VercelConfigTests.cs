namespace SaasTemplate.Api.Tests.Infrastructure;

public class VercelConfigTests
{
    [Fact]
    public void VercelJson_DoesNotContain_IgnoreCommand()
    {
        // Find vercel.json relative to the solution root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "vercel.json")))
            dir = dir.Parent;

        Assert.NotNull(dir); // vercel.json must exist somewhere above the test binary

        var content = File.ReadAllText(Path.Combine(dir.FullName, "vercel.json"));
        Assert.DoesNotContain("ignoreCommand", content);
    }
}
