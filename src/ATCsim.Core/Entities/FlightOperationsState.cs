using ATCsim.Core.Kinematics;

namespace ATCsim.Core.Entities;

public enum FlightPhase { EnRoute, Departing, ArrivalQueue, Holding, Approach, Final, MissedApproach, Landed }

public sealed class FlightOperationsState
{
    public FlightPhase Phase { get; set; }
    public DateTime? RunwayClearAt { get; set; }
    public string? RunwayDesignator { get; set; }
    public int RunwayHeading { get; set; }
    public DateTime? ArrivalQueuedAt { get; set; }
    public Vector2D[]? HoldingPattern { get; set; }
    public int HoldingLeg { get; set; }
    public bool ManualHold { get; set; }
    public decimal HoldingAltitude { get; set; }
    public bool ApproachAligned { get; set; }
}
