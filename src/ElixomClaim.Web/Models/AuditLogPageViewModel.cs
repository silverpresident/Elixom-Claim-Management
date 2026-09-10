namespace ElixomClaim.Web.Models;

public sealed record AuditLogPageViewModel(
    IReadOnlyList<AuditRecordViewModel> Records,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
