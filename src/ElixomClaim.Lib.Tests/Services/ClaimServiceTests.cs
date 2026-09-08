using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class ClaimServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UserClaimQueries_ReturnOnlyUnresolvedDashboardClaims_AndPageActorHistory()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        db.Claims.AddRange(
            new Claim { ClaimantUserId = userId, Title = "Draft", Description = "", Amount = 1m, Status = ClaimStatus.Draft, CreatedAtUtc = DateTime.UtcNow },
            new Claim { ClaimantUserId = userId, Title = "Submitted", Description = "", Amount = 1m, Status = ClaimStatus.Submitted, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            new Claim { ClaimantUserId = userId, Title = "Processing", Description = "", Amount = 1m, Status = ClaimStatus.Accepted, PaymentStatus = ClaimPaymentStatus.Processing, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2) },
            new Claim { ClaimantUserId = userId, Title = "Paid", Description = "", Amount = 1m, Status = ClaimStatus.Accepted, PaymentStatus = ClaimPaymentStatus.Paid, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3) },
            new Claim { ClaimantUserId = userId, Title = "Deleted", Description = "", Amount = 1m, Status = ClaimStatus.Draft, IsDeleted = true, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4) },
            new Claim { ClaimantUserId = otherUserId, Title = "Other", Description = "", Amount = 1m, Status = ClaimStatus.Draft, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);

        var unresolved = await service.GetUserUnresolvedClaimsAsync(userId);
        var history = await service.GetUserClaimHistoryAsync(userId, page: 2, pageSize: 2);

        Assert.Equal(new[] { "Draft", "Submitted", "Processing" }, unresolved.Select(claim => claim.Title));
        Assert.Equal(4, history.TotalCount);
        Assert.Equal(2, history.Page);
        Assert.Equal(2, history.Claims.Count);
        Assert.DoesNotContain(history.Claims, claim => claim.ClaimantUserId == otherUserId || claim.IsDeleted);
    }

    [Fact]
    public async Task CreateDraftAsync_CreatesClaimInDraftStatus()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var userId = Guid.NewGuid();
        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Taxi Fare", "Client meeting travel", 1500.00m));

        Assert.NotNull(claim);
        Assert.Equal(ClaimStatus.Draft, claim.Status);
        Assert.Equal(ClaimPaymentStatus.Unpaid, claim.PaymentStatus);
        Assert.Equal("Taxi Fare", claim.Title);
        Assert.Equal(1500.00m, claim.Amount);
        Assert.True(claim.SequenceNo > 0);
    }

    [Fact]
    public async Task SubmitAsync_TransitionsDraftToSubmitted()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var userId = Guid.NewGuid();
        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Taxi Fare", "Client meeting travel", 1500.00m));

        var success = await claimService.SubmitAsync(new SubmitClaimCommand(claim.Id, userId));
        Assert.True(success);

        var updatedClaim = await db.Claims.FindAsync(claim.Id);
        Assert.NotNull(updatedClaim);
        Assert.Equal(ClaimStatus.Submitted, updatedClaim.Status);
    }

    [Fact]
    public async Task AcceptAsync_TransitionsSubmittedToAccepted()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var userId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Taxi Fare", "Client meeting travel", 1500.00m));
        await claimService.SubmitAsync(new SubmitClaimCommand(claim.Id, userId));

        var success = await claimService.AcceptAsync(new AcceptClaimCommand(claim.Id, managerId));
        Assert.True(success);

        var updatedClaim = await db.Claims.FindAsync(claim.Id);
        Assert.NotNull(updatedClaim);
        Assert.Equal(ClaimStatus.Accepted, updatedClaim.Status);
    }

    [Fact]
    public async Task GetByIdAsync_HidesPrivateComments_ForNonManagerUsers()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var claimant = new User { Id = Guid.NewGuid(), Email = "user@elixom.com", FullName = "User", Role = UserRole.User, IsActive = true };
        var manager = new User { Id = Guid.NewGuid(), Email = "mgr@elixom.com", FullName = "Manager", Role = UserRole.Manager, IsActive = true };
        db.Users.AddRange(claimant, manager);
        await db.SaveChangesAsync();

        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(claimant.Id, "Taxi Fare", "Travel", 1000m));
        await claimService.AddCommentAsync(new AddClaimCommentCommand(claim.Id, claimant.Id, "Public Note", IsPrivate: false));
        await claimService.AddCommentAsync(new AddClaimCommentCommand(claim.Id, manager.Id, "Private Manager Note", IsPrivate: true));

        Assert.All(await db.ClaimComments.ToListAsync(), comment => Assert.True(comment.SequenceNo > 0));

        // Claimant retrieves claim
        var claimantView = await claimService.GetByIdAsync(claim.Id, claimant);
        Assert.NotNull(claimantView);
        Assert.Single(claimantView.Comments);
        Assert.Equal("Public Note", claimantView.Comments.First().Content);

        // Manager retrieves claim
        var managerView = await claimService.GetByIdAsync(claim.Id, manager);
        Assert.NotNull(managerView);
        Assert.Equal(2, managerView.Comments.Count);
    }
}
