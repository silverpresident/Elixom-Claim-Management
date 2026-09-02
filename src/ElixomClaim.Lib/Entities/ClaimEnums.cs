namespace ElixomClaim.Lib.Entities;

public enum ClaimStatus
{
    Draft = 1,
    Submitted = 2,
    Accepted = 3,
    Rejected = 4
}

public enum ClaimPaymentStatus
{
    Unpaid = 1,
    Processing = 2,
    Paid = 3
}
