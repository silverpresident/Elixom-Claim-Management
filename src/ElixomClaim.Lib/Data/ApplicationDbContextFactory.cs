using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ElixomClaim.Lib.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ElixomClaimDb;Trusted_Connection=True;MultipleActiveResultSets=true", sqlOptions =>
        {
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.DefaultSchema);
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
