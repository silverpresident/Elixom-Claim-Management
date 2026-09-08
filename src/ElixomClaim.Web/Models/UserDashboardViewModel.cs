using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Web.Models;

public class UserDashboardViewModel
{
    public IEnumerable<Claim> Claims { get; set; } = Enumerable.Empty<Claim>();
    public IEnumerable<JobPayment> PaymentHistory { get; set; } = Enumerable.Empty<JobPayment>();
}

public sealed record ClaimHistoryViewModel(IReadOnlyList<Claim> Claims, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
