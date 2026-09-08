using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public sealed class ClaimServiceQueueTests
{
    [Fact]
    public async Task Queue_ExcludesSoftDeletedClaimsAndLimitsSubmittedQueueToUnpaidClaims()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var claimant = new User { Email = "claimant@example.test", NormalizedEmail = "CLAIMANT@EXAMPLE.TEST" };
        db.Users.Add(claimant);
        db.Claims.AddRange(
            new Claim { ClaimantUser = claimant, Title = "Eligible", Description = "", Amount = 1m, Status = ClaimStatus.Submitted, PaymentStatus = ClaimPaymentStatus.Unpaid },
            new Claim { ClaimantUser = claimant, Title = "Paid", Description = "", Amount = 1m, Status = ClaimStatus.Submitted, PaymentStatus = ClaimPaymentStatus.Paid },
            new Claim { ClaimantUser = claimant, Title = "Deleted", Description = "", Amount = 1m, Status = ClaimStatus.Submitted, PaymentStatus = ClaimPaymentStatus.Unpaid, IsDeleted = true });
        await db.SaveChangesAsync();
        var service = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);

        var queue = await service.GetQueueClaimsAsync(ClaimStatus.Submitted);

        var claim = Assert.Single(queue);
        Assert.Equal("Eligible", claim.Title);
    }
}
