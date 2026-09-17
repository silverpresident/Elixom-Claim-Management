using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface ICollectionClientAdministrationService
{
    Task<Result<CollectionClient>> CreateClientAsync(CreateCollectionClientCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionClient>> UpdateClientAsync(UpdateCollectionClientCommand command, CancellationToken cancellationToken = default);
    Task<Result> SetClientActiveAsync(SetCollectionClientActiveCommand command, CancellationToken cancellationToken = default);
    Task<Result> AssignUserAsync(AssignCollectionClientUserCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveUserAsync(RemoveCollectionClientUserCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionPurposeOption>> AddPurposeOptionAsync(AddCollectionPurposeOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionPurposeOption>> UpdatePurposeOptionAsync(UpdateCollectionPurposeOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result> SetPurposeOptionActiveAsync(SetCollectionPurposeOptionActiveCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionAmountOption>> AddAmountOptionAsync(AddCollectionAmountOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionAmountOption>> UpdateAmountOptionAsync(UpdateCollectionAmountOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result> SetAmountOptionActiveAsync(SetCollectionAmountOptionActiveCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionClientBankDetail>> AddBankDetailAsync(AddCollectionClientBankDetailCommand command, CancellationToken cancellationToken = default);
}

public record CreateCollectionClientCommand(Guid ActorUserId, string Name, string? Description = null, string? Notes = null, decimal PerJobProcessingFee = 0, decimal PerTransactionFee = 0);
public record UpdateCollectionClientCommand(Guid ActorUserId, Guid CollectionClientId, string Name, string? Description, string? Notes, decimal PerJobProcessingFee, decimal PerTransactionFee);
public record SetCollectionClientActiveCommand(Guid ActorUserId, Guid CollectionClientId, bool IsActive);
public record AssignCollectionClientUserCommand(Guid ActorUserId, Guid CollectionClientId, Guid UserId);
public record RemoveCollectionClientUserCommand(Guid ActorUserId, Guid CollectionClientId, Guid UserId);
public record AddCollectionPurposeOptionCommand(Guid ActorUserId, Guid CollectionClientId, string Name, int DisplayOrder);
public record UpdateCollectionPurposeOptionCommand(Guid ActorUserId, Guid CollectionClientId, Guid PurposeOptionId, string Name, int DisplayOrder);
public record SetCollectionPurposeOptionActiveCommand(Guid ActorUserId, Guid CollectionClientId, Guid PurposeOptionId, bool IsActive);
public record AddCollectionAmountOptionCommand(Guid ActorUserId, Guid CollectionClientId, string Name, decimal Amount, int DisplayOrder);
public record UpdateCollectionAmountOptionCommand(Guid ActorUserId, Guid CollectionClientId, Guid AmountOptionId, string Name, decimal Amount, int DisplayOrder);
public record SetCollectionAmountOptionActiveCommand(Guid ActorUserId, Guid CollectionClientId, Guid AmountOptionId, bool IsActive);
public record AddCollectionClientBankDetailCommand(
    Guid ActorUserId,
    Guid CollectionClientId,
    string AccountName,
    string BankName,
    string BranchCode,
    string BranchName,
    string AccountType,
    string AccountNumber,
    string? Notes = null);
