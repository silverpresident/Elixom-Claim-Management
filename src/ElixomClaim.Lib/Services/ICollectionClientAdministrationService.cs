using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface ICollectionClientAdministrationService
{
    Task<Result<CollectionClient>> CreateClientAsync(CreateCollectionClientCommand command, CancellationToken cancellationToken = default);
    Task<Result> AssignUserAsync(AssignCollectionClientUserCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveUserAsync(RemoveCollectionClientUserCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionPurposeOption>> AddPurposeOptionAsync(AddCollectionPurposeOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionAmountOption>> AddAmountOptionAsync(AddCollectionAmountOptionCommand command, CancellationToken cancellationToken = default);
    Task<Result<CollectionClientBankDetail>> AddBankDetailAsync(AddCollectionClientBankDetailCommand command, CancellationToken cancellationToken = default);
}

public record CreateCollectionClientCommand(Guid ActorUserId, string Name);
public record AssignCollectionClientUserCommand(Guid ActorUserId, Guid CollectionClientId, Guid UserId);
public record RemoveCollectionClientUserCommand(Guid ActorUserId, Guid CollectionClientId, Guid UserId);
public record AddCollectionPurposeOptionCommand(Guid ActorUserId, Guid CollectionClientId, string Name, int DisplayOrder);
public record AddCollectionAmountOptionCommand(Guid ActorUserId, Guid CollectionClientId, string Name, decimal Amount, int DisplayOrder);
public record AddCollectionClientBankDetailCommand(
    Guid ActorUserId,
    Guid CollectionClientId,
    string AccountName,
    string BankName,
    string BranchCode,
    string AccountNumber);
