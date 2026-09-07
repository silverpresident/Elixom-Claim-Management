using ElixomClaim.Web.Mcp.Tools;
using ModelContextProtocol.Server;
using System.Reflection;

namespace ElixomClaim.Web.Tests.Controllers;

public class McpToolContractTests
{
    [Fact]
    public void DomainToolTypes_ExposeOnlyTheStableAttributedToolNames()
    {
        var toolTypes = new[]
        {
            typeof(ClaimTools), typeof(CollectionTools), typeof(JobPaymentTools),
            typeof(PayrollTools), typeof(EmailTools), typeof(OperationsTools)
        };

        Assert.All(toolTypes, type => Assert.NotNull(type.GetCustomAttribute<McpServerToolTypeAttribute>()));

        var names = toolTypes.SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .Order()
            .ToArray();

        Assert.Equal(new[]
        {
            "claims_get", "claims_list", "claims_submit", "collections_get", "collections_list",
            "email_preview", "email_queue", "job_payments_get", "job_payments_list",
            "operations_outbox_wakeup", "operations_salary_generation", "operations_status",
            "payroll_preview", "payroll_run"
        }, names);
    }

    [Fact]
    public void CollectionToolDto_DoesNotExposePayorEmailOrInternalProcessingFee()
    {
        var propertyNames = typeof(CollectionDto).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("PayorEmail", propertyNames);
        Assert.DoesNotContain("ProcessingFee", propertyNames);
    }
}
