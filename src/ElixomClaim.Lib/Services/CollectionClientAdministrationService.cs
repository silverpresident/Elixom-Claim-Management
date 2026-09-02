using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class CollectionClientAdministrationService : ICollectionClientAdministrationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ISystemClock _clock;
    private readonly ILogger<CollectionClientAdministrationService> _logger;

    public CollectionClientAdministrationService(ApplicationDbContext dbContext, IAuditService auditService, ISystemClock clock, ILogger<CollectionClientAdministrationService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CollectionClient>> CreateClientAsync(CreateCollectionClientCommand command, CancellationToken cancellationToken = default)
    {
        var authorization = await EnsureAdministratorAsync(command.ActorUserId, cancellationToken);
        if (authorization.IsFailure || string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<CollectionClient>(authorization.IsFailure ? authorization.Error : "Client name is required.");

        var name = command.Name.Trim();
        if (await _dbContext.CollectionClients.AnyAsync(c => c.Name == name, cancellationToken))
            return Result.Failure<CollectionClient>("A collection client with that name already exists.");

        var client = new CollectionClient { Name = name, CreatedAtUtc = _clock.UtcNow, UpdatedAtUtc = _clock.UtcNow };
        _dbContext.CollectionClients.Add(client);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("COLLECTION_CLIENT_CREATED", $"CollectionClient:{client.Id}", command.ActorUserId, new { client.Id, client.Name }, cancellationToken);
        return Result.Success(client);
    }

    public async Task<Result> AssignUserAsync(AssignCollectionClientUserCommand command, CancellationToken cancellationToken = default)
    {
        var authorization = await EnsureAdministratorAsync(command.ActorUserId, cancellationToken);
        if (authorization.IsFailure) return authorization;
        if (!await _dbContext.CollectionClients.AnyAsync(c => c.Id == command.CollectionClientId, cancellationToken) ||
            !await _dbContext.Users.AnyAsync(u => u.Id == command.UserId && u.IsActive, cancellationToken))
            return Result.Failure("The client or active user was not found.");
        if (await _dbContext.CollectionClientUsers.AnyAsync(a => a.CollectionClientId == command.CollectionClientId && a.UserId == command.UserId, cancellationToken))
            return Result.Failure("The user is already assigned to this client.");

        _dbContext.CollectionClientUsers.Add(new CollectionClientUser { CollectionClientId = command.CollectionClientId, UserId = command.UserId, AssignedAtUtc = _clock.UtcNow });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("COLLECTION_CLIENT_USER_ASSIGNED", $"CollectionClient:{command.CollectionClientId}", command.ActorUserId, new { command.UserId }, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveUserAsync(RemoveCollectionClientUserCommand command, CancellationToken cancellationToken = default)
    {
        var authorization = await EnsureAdministratorAsync(command.ActorUserId, cancellationToken);
        if (authorization.IsFailure) return authorization;
        var assignment = await _dbContext.CollectionClientUsers.FindAsync([command.CollectionClientId, command.UserId], cancellationToken);
        if (assignment is null) return Result.Failure("The user is not assigned to this client.");

        _dbContext.CollectionClientUsers.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("COLLECTION_CLIENT_USER_REMOVED", $"CollectionClient:{command.CollectionClientId}", command.ActorUserId, new { command.UserId }, cancellationToken);
        return Result.Success();
    }

    public Task<Result<CollectionPurposeOption>> AddPurposeOptionAsync(AddCollectionPurposeOptionCommand command, CancellationToken cancellationToken = default) =>
        AddOptionAsync(command.ActorUserId, command.CollectionClientId, command.Name, command.DisplayOrder,
            async name =>
            {
                var option = new CollectionPurposeOption { CollectionClientId = command.CollectionClientId, Name = name, DisplayOrder = command.DisplayOrder };
                _dbContext.CollectionPurposeOptions.Add(option);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return option;
            }, "COLLECTION_PURPOSE_OPTION_ADDED", cancellationToken);

    public async Task<Result<CollectionAmountOption>> AddAmountOptionAsync(AddCollectionAmountOptionCommand command, CancellationToken cancellationToken = default)
    {
        var authorization = await EnsureAdministratorAsync(command.ActorUserId, cancellationToken);
        if (authorization.IsFailure || string.IsNullOrWhiteSpace(command.Name) || command.Amount <= 0)
            return Result.Failure<CollectionAmountOption>(authorization.IsFailure ? authorization.Error : "Option name and a positive amount are required.");
        if (!await ClientExistsAsync(command.CollectionClientId, cancellationToken)) return Result.Failure<CollectionAmountOption>("Collection client not found.");
        var name = command.Name.Trim();
        if (await _dbContext.CollectionAmountOptions.AnyAsync(o => o.CollectionClientId == command.CollectionClientId && o.Name == name, cancellationToken))
            return Result.Failure<CollectionAmountOption>("This amount option already exists for the client.");

        var option = new CollectionAmountOption { CollectionClientId = command.CollectionClientId, Name = name, Amount = command.Amount, DisplayOrder = command.DisplayOrder };
        _dbContext.CollectionAmountOptions.Add(option);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("COLLECTION_AMOUNT_OPTION_ADDED", $"CollectionClient:{command.CollectionClientId}", command.ActorUserId, new { option.Id, option.Name, option.Amount }, cancellationToken);
        return Result.Success(option);
    }

    public async Task<Result<CollectionClientBankDetail>> AddBankDetailAsync(AddCollectionClientBankDetailCommand command, CancellationToken cancellationToken = default)
    {
        var authorization = await EnsureAdministratorAsync(command.ActorUserId, cancellationToken);
        if (authorization.IsFailure) return Result.Failure<CollectionClientBankDetail>(authorization.Error);
        if (!await ClientExistsAsync(command.CollectionClientId, cancellationToken)) return Result.Failure<CollectionClientBankDetail>("Collection client not found.");
        if (new[] { command.AccountName, command.BankName, command.BranchCode, command.AccountNumber }.Any(string.IsNullOrWhiteSpace))
            return Result.Failure<CollectionClientBankDetail>("All bank detail fields are required.");

        var detail = new CollectionClientBankDetail { CollectionClientId = command.CollectionClientId, AccountName = command.AccountName.Trim(), BankName = command.BankName.Trim(), BranchCode = command.BranchCode.Trim(), AccountNumber = command.AccountNumber.Trim(), CreatedAtUtc = _clock.UtcNow };
        _dbContext.CollectionClientBankDetails.Add(detail);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("COLLECTION_CLIENT_BANK_DETAIL_ADDED", $"CollectionClient:{command.CollectionClientId}", command.ActorUserId, new { detail.Id }, cancellationToken);
        return Result.Success(detail);
    }

    private async Task<Result<CollectionPurposeOption>> AddOptionAsync(Guid actorUserId, Guid clientId, string inputName, int displayOrder, Func<string, Task<CollectionPurposeOption>> create, string auditAction, CancellationToken cancellationToken)
    {
        var authorization = await EnsureAdministratorAsync(actorUserId, cancellationToken);
        if (authorization.IsFailure || string.IsNullOrWhiteSpace(inputName)) return Result.Failure<CollectionPurposeOption>(authorization.IsFailure ? authorization.Error : "Option name is required.");
        if (!await ClientExistsAsync(clientId, cancellationToken)) return Result.Failure<CollectionPurposeOption>("Collection client not found.");
        var name = inputName.Trim();
        if (await _dbContext.CollectionPurposeOptions.AnyAsync(o => o.CollectionClientId == clientId && o.Name == name, cancellationToken)) return Result.Failure<CollectionPurposeOption>("This purpose option already exists for the client.");
        var option = await create(name);
        await AuditAsync(auditAction, $"CollectionClient:{clientId}", actorUserId, new { option.Id, option.Name }, cancellationToken);
        return Result.Success(option);
    }

    private async Task<Result> EnsureAdministratorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Users.Where(u => u.Id == actorUserId && u.IsActive).Select(u => (UserRole?)u.Role).SingleOrDefaultAsync(cancellationToken);
        return role is UserRole.Administrator ? Result.Success() : Result.Failure("Administrator access is required.");
    }

    private Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken) => _dbContext.CollectionClients.AnyAsync(c => c.Id == clientId, cancellationToken);

    private async Task AuditAsync(string action, string target, Guid actorUserId, object afterState, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(action, target, afterState: afterState, actorUserId: actorUserId.ToString(), cancellationToken: cancellationToken);
        _logger.LogInformation("Collection client configuration updated: {Action} {Target}", action, target);
    }
}
