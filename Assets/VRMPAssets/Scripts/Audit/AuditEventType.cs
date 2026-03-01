/// <summary>
/// Enumeration of all audit event types that can be logged.
/// </summary>
public enum AuditEventType
{
    SESSION_START,
    SESSION_END,
    ENTER_OFFICE,
    EXIT_OFFICE,
    JOIN_MEETING,
    LEAVE_MEETING,
    APPLE_PICKED,
    APPLE_DROPPED,
    APPLE_ADDED_TO_INVENTORY,
    APPLE_REMOVED_FROM_INVENTORY,
    POLL_VOTE,
    ERROR,
    // Smart Farm / VR Agriculture Platform
    FARM_EVENT,
    FARM_VOTE_OPENED,
    FARM_IRRIGATION_CHANGED,
    FARM_TEMPERATURE_CHANGED,
    FARM_PLANT_STAGE_CHANGED
}
