using ElixomClaim.Lib.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class MigrationValidationTests
{
    [Fact]
    public void DbContextModel_HasDefaultSchemaDbClaim()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("Migration_Validation_Db")
            .Options;

        using var context = new ApplicationDbContext(options);
        var defaultSchema = context.Model.GetDefaultSchema();

        Assert.Equal("dbclaim", defaultSchema);
    }

    [Fact]
    public void MigrationFiles_DoNotContainDestructiveDropOperationsInUpMethod()
    {
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Lib", "Migrations");
        if (!Directory.Exists(migrationsPath))
        {
            // Fallback for execution from test directory
            migrationsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "ElixomClaim.Lib", "Migrations"));
        }

        Assert.True(Directory.Exists(migrationsPath), $"Migrations directory not found at: {migrationsPath}");

        var csFiles = Directory.GetFiles(migrationsPath, "*.cs", SearchOption.TopDirectoryOnly);
        foreach (var file in csFiles)
        {
            if (file.EndsWith("Designer.cs") || file.EndsWith("Snapshot.cs"))
                continue;

            var content = File.ReadAllText(file);
            // Extract the Up method content to verify no destructive operations are in Up migration steps
            var upMethodStartIndex = content.IndexOf("protected override void Up(");
            var downMethodStartIndex = content.IndexOf("protected override void Down(");

            if (upMethodStartIndex >= 0)
            {
                var upContent = downMethodStartIndex > upMethodStartIndex
                    ? content.Substring(upMethodStartIndex, downMethodStartIndex - upMethodStartIndex)
                    : content.Substring(upMethodStartIndex);

                Assert.DoesNotContain("DropTable", upContent);
                Assert.DoesNotContain("DropColumn", upContent);
            }
        }
    }
}
