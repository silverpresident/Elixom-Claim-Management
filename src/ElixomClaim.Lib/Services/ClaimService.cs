using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class ClaimService : IClaimService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ClaimService> _logger;

    public ClaimService(ApplicationDbContext dbContext, IAuditService auditService, ILogger<ClaimService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Claim> CreateDraftAsync(CreateClaimCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
        {
            throw new ArgumentException("Claim amount must be greater than zero.", nameof(command.Amount));
        }

        var claim = new Claim
        {
            ClaimantUserId = command.ClaimantUserId,
            Title = command.Title.Trim(),
            Description = command.Description.Trim(),
            Amount = command.Amount,
            Currency = "JMD",
            Status = ClaimStatus.Draft,
            PaymentStatus = ClaimPaymentStatus.Unpaid,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_DRAFT_CREATED",
            target: $"Claim:{claim.Id}",
            afterState: new { claim.Id, claim.Title, claim.Amount, claim.Status },
            actorUserId: command.ClaimantUserId.ToString(),
            cancellationToken: cancellationToken);

        return claim;
    }

    public async Task<Claim?> EditDraftAsync(EditClaimCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return null;
        }

        if (claim.ClaimantUserId != command.ActorUserId)
        {
            throw new UnauthorizedAccessException("Only the claim owner can edit a draft claim.");
        }

        if (claim.Status != ClaimStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft claims can be edited. Current status: {claim.Status}");
        }

        var beforeState = new { claim.Title, claim.Description, claim.Amount };

        claim.Title = command.Title.Trim();
        claim.Description = command.Description.Trim();
        claim.Amount = command.Amount;
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_DRAFT_EDITED",
            target: $"Claim:{claim.Id}",
            beforeState: beforeState,
            afterState: new { claim.Title, claim.Description, claim.Amount },
            actorUserId: command.ActorUserId.ToString(),
            cancellationToken: cancellationToken);

        return claim;
    }

    public async Task<bool> SubmitAsync(SubmitClaimCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return false;
        }

        if (claim.ClaimantUserId != command.ActorUserId)
        {
            throw new UnauthorizedAccessException("Only the claim owner can submit a claim.");
        }

        if (claim.Status != ClaimStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft claims can be submitted. Current status: {claim.Status}");
        }

        claim.Status = ClaimStatus.Submitted;
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_SUBMITTED",
            target: $"Claim:{claim.Id}",
            afterState: new { claim.Id, claim.Status },
            actorUserId: command.ActorUserId.ToString(),
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> AcceptAsync(AcceptClaimCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return false;
        }

        if (claim.Status != ClaimStatus.Submitted)
        {
            throw new InvalidOperationException($"Only Submitted claims can be accepted. Current status: {claim.Status}");
        }

        claim.Status = ClaimStatus.Accepted;
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_ACCEPTED",
            target: $"Claim:{claim.Id}",
            afterState: new { claim.Id, claim.Status },
            actorUserId: command.ActorUserId.ToString(),
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> RejectAsync(RejectClaimCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return false;
        }

        if (claim.Status != ClaimStatus.Submitted)
        {
            throw new InvalidOperationException($"Only Submitted claims can be rejected. Current status: {claim.Status}");
        }

        if (string.IsNullOrWhiteSpace(command.RejectionReason))
        {
            throw new ArgumentException("Rejection reason is required when rejecting a claim.", nameof(command.RejectionReason));
        }

        claim.Status = ClaimStatus.Rejected;
        claim.RejectionReason = command.RejectionReason.Trim();
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_REJECTED",
            target: $"Claim:{claim.Id}",
            afterState: new { claim.Id, claim.Status, claim.RejectionReason },
            actorUserId: command.ActorUserId.ToString(),
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteClaimCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return false;
        }

        if (claim.ClaimantUserId != command.ActorUserId)
        {
            throw new UnauthorizedAccessException("Only the claim owner can delete a draft claim.");
        }

        if (claim.Status != ClaimStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft claims can be soft-deleted. Current status: {claim.Status}");
        }

        claim.IsDeleted = true;
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_SOFT_DELETED",
            target: $"Claim:{claim.Id}",
            actorUserId: command.ActorUserId.ToString(),
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<ClaimComment?> AddCommentAsync(AddClaimCommentCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim == null)
        {
            return null;
        }

        var comment = new ClaimComment
        {
            ClaimId = command.ClaimId,
            AuthorUserId = command.AuthorUserId,
            Content = command.Content.Trim(),
            IsPrivate = command.IsPrivate,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ClaimComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLAIM_COMMENT_ADDED",
            target: $"Claim:{claim.Id}",
            afterState: new { comment.Id, comment.IsPrivate },
            actorUserId: command.AuthorUserId.ToString(),
            cancellationToken: cancellationToken);

        return comment;
    }

    public async Task<Claim?> GetByIdAsync(long claimId, User actor, CancellationToken cancellationToken = default)
    {
        var claim = await _dbContext.Claims
            .AsNoTracking()
            .Include(c => c.ClaimantUser)
            .Include(c => c.Comments.OrderBy(cc => cc.CreatedAtUtc))
                .ThenInclude(cc => cc.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        if (claim == null)
        {
            return null;
        }

        // Non-management users can only view their own claims
        var isManagerOrAbove = actor.Role.HasMinimumRole(UserRole.Manager);
        if (!isManagerOrAbove && claim.ClaimantUserId != actor.Id)
        {
            return null;
        }

        // Filter out private comments for non-manager/accountant/admin users
        if (!isManagerOrAbove)
        {
            claim.Comments = claim.Comments.Where(cc => !cc.IsPrivate).ToList();
        }

        return claim;
    }

    public async Task<List<Claim>> GetUserClaimsAsync(Guid claimantUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Claims
            .Where(c => c.ClaimantUserId == claimantUserId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Claim>> GetQueueClaimsAsync(ClaimStatus? filterStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Claims
            .Include(c => c.ClaimantUser)
            .AsQueryable();

        if (filterStatus.HasValue)
        {
            query = query.Where(c => c.Status == filterStatus.Value);
        }

        return await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
    }
}
