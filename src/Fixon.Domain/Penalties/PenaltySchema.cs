namespace Fixon.Domain.Penalties;

public enum PenaltyType
{
    Fixed = 1,
    Percent = 2,
    TimeBased = 3,
}

public enum PenaltyRounding
{
    None = 0,
    Floor = 1,
    Ceil = 2,
    Round = 3,
}

public enum BaseAmountSource
{
    ContractValue = 1,
    ShipmentValue = 2,
    Custom = 3,
}

