namespace Content.Server._Persistence14.Requisitions;

// Runtime marker on a lathe: requisition prints it currently owes. When a finished item matches a job, the
// console's in-progress count drops and (if the job is bound for a flatpacker) the board is moved into the
// console's internal flatpack storage. If the lathe loses power mid-print, the outstanding jobs' contributed
// materials are returned to the customer. Not persisted.
[RegisterComponent]
public sealed partial class RequisitionsLatheJobComponent : Component
{
    public List<RequisitionJob> Jobs = new();
}

// One in-progress requisition print on a lathe.
public struct RequisitionJob
{
    // The recipe being printed (matched against finished items).
    public string Recipe;

    // The console that took the order.
    public EntityUid Console;

    // Materials the customer contributed toward this job, returned to them if the print fails.
    public Dictionary<string, int>? Cover;

    // If set, the finished board is routed into the console's flatpack storage instead of delivered.
    public EntityUid? Flatpacker;
}
