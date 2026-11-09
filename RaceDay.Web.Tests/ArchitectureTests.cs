using System.Text.RegularExpressions;

namespace RaceDay.Web.Tests;

/// <summary>
/// Guards the rule that matters most in Part 3: the MVC project has no database access and never talks to
/// Azure. If someone adds EF Core, SqlClient or the Azure SDK to RaceDay.Web, these tests fail.
/// </summary>
public class ArchitectureTests
{
    private static string WebProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "RaceDay.Web", "RaceDay.Web.csproj");
            if (File.Exists(candidate)) return Path.GetDirectoryName(candidate)!;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the RaceDay.Web project folder above " + AppContext.BaseDirectory);
    }

    // Packages the MVC project may reference. System.Net.Http.Json ships inside .NET itself, so it adds no capability.
    private static readonly string[] AllowedPackages = ["System.Net.Http.Json"];

    [Fact]
    public void WebProject_ReferencesOnlyAllowListedPackages()
    {
        var csproj = File.ReadAllText(Path.Combine(WebProjectDirectory(), "RaceDay.Web.csproj"));

        var referenced = Regex.Matches(csproj, "<PackageReference\\s+Include=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.All(referenced, name => Assert.Contains(name, AllowedPackages));
        Assert.DoesNotContain("<ProjectReference", csproj);   // and it must not reference the API project either
    }

    [Theory]
    [InlineData("Azure")]
    [InlineData("EntityFramework")]
    [InlineData("SqlClient")]
    public void WebProject_NeverReferencesDatabaseOrAzurePackages(string forbidden)
    {
        var csproj = File.ReadAllText(Path.Combine(WebProjectDirectory(), "RaceDay.Web.csproj"));
        var references = string.Join("\n", Regex.Matches(csproj, "<PackageReference[^>]*>").Select(m => m.Value));

        Assert.DoesNotContain(forbidden, references, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebProject_SourceNeverUsesDatabaseOrAzureTypes()
    {
        var root = WebProjectDirectory();
        var forbidden = new Regex(
            @"^\s*using\s+(Azure|Microsoft\.EntityFrameworkCore|Microsoft\.Data\.SqlClient|System\.Data\.SqlClient)\b|\bDbContext\b|\bBlobServiceClient\b|\bBlobClient\b",
            RegexOptions.Multiline);

        var offenders = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => forbidden.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void WebConfiguration_DoesNotContainAConnectionStringOrAzureKey()
    {
        var settings = File.ReadAllText(Path.Combine(WebProjectDirectory(), "appsettings.json"));

        Assert.DoesNotContain("ConnectionStrings", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountKey", settings, StringComparison.OrdinalIgnoreCase);
    }
}
