namespace Fixon.Domain.Claims;

// Важно: не переупорядочивать значения без миграционного плана.
public enum ClaimStatus
{
    Draft = 1,
    Submitted = 2,  // was Confirmed
    Resolved = 3,   // was Closed
    UnderReview = 4,
    Accepted = 5,
    Rejected = 6,
    Disputed = 7,
    Cancelled = 8,
}


