using Xunit;

namespace ElixomClaim.Lib.Tests.Governance;

public class GovernanceValidationTests
{
    [Fact]
    public void AdrTemplateAndIndex_ExistAndAreConfigured()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var templatePath = Path.Combine(rootDir, "adr", "template.md");
        var indexPath = Path.Combine(rootDir, "adr", "index.md");

        Assert.True(File.Exists(templatePath), $"ADR template not found at {templatePath}");
        Assert.True(File.Exists(indexPath), $"ADR index not found at {indexPath}");

        var templateContent = File.ReadAllText(templatePath);
        Assert.Contains("Status:", templateContent);
        Assert.Contains("Context", templateContent);
        Assert.Contains("Decision", templateContent);
        Assert.Contains("Consequences", templateContent);
    }

    [Fact]
    public void GitHubWorkflowAndPrTemplate_ExistAndAreConfigured()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var workflowPath = Path.Combine(rootDir, ".github", "workflows", "ci.yml");
        var prTemplatePath = Path.Combine(rootDir, ".github", "pull_request_template.md");

        Assert.True(File.Exists(workflowPath), $"GitHub Actions workflow not found at {workflowPath}");
        Assert.True(File.Exists(prTemplatePath), $"Pull request template not found at {prTemplatePath}");

        var workflowContent = File.ReadAllText(workflowPath);
        Assert.Contains("ElixomClaim.slnx", workflowContent);
        Assert.Contains("dotnet test", workflowContent);

        var prTemplateContent = File.ReadAllText(prTemplatePath);
        Assert.Contains("Verification Checklist", prTemplateContent);
        Assert.Contains("dbclaim", prTemplateContent);
    }
}
