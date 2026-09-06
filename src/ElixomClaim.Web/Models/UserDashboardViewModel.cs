using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Web.Models;

public class UserDashboardViewModel
{
    public IEnumerable<Claim> Claims { get; set; } = Enumerable.Empty<Claim>();
    public IEnumerable<JobPayment> PaymentHistory { get; set; } = Enumerable.Empty<JobPayment>();
}
