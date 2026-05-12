using Corvus.Text.Json;
using NumericEnumerations.Models;

// Use static const instances instead of parsing
Status pending = Status.Pending.ConstInstance;    // value: 1
Status active = Status.Active.ConstInstance;      // value: 2
Status complete = Status.Complete.ConstInstance;  // value: 3

Console.WriteLine("Status values using const instances:");
Console.WriteLine($"  {pending}: {DescribeStatus(pending)}");
Console.WriteLine($"  {active}: {DescribeStatus(active)}");
Console.WriteLine($"  {complete}: {DescribeStatus(complete)}");
Console.WriteLine();

// Demonstrate parsing from JSON
Console.WriteLine("Parsing status values from JSON:");
string pendingJson = "1";
using var parsedPending = ParsedJsonDocument<Status>.Parse(pendingJson);
Status parsedPendingStatus = parsedPending.RootElement;
Console.WriteLine($"  Parsed '{pendingJson}': {DescribeStatus(parsedPendingStatus)}");
Console.WriteLine();

// Demonstrate handling invalid values
Console.WriteLine("Testing validation:");
using var invalidDoc = ParsedJsonDocument<Status>.Parse("99");
Status invalid = invalidDoc.RootElement;
Console.WriteLine($"  Status {invalid} is valid: {invalid.EvaluateSchema()}");
Console.WriteLine();

// Pattern matching with context (state parameter)
Console.WriteLine("Pattern matching with context:");
int requestCount = 5;
Console.WriteLine($"  {pending} with {requestCount} requests: {ProcessStatus(pending, requestCount)}");
Console.WriteLine($"  {active} with {requestCount} requests: {ProcessStatus(active, requestCount)}");
Console.WriteLine($"  {complete} with {requestCount} requests: {ProcessStatus(complete, requestCount)}");

// Try to process invalid status - will hit default match
try
{
    Console.WriteLine($"  {invalid} with {requestCount} requests: {ProcessStatus(invalid, requestCount)}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

// Pattern matching function without context
string DescribeStatus(in Status status)
{
    return status.Match(
        matchPending: static (in Status.Pending _) => "Pending - waiting to start",
        matchActive: static (in Status.Active _) => "Active - currently running",
        matchComplete: static (in Status.Complete _) => "Complete - finished",
        defaultMatch: static (in Status _) => "Unknown status");
}

// Pattern matching function WITH context parameter
string ProcessStatus(in Status status, int requestCount)
{
    return status.Match(
        requestCount,  // context parameter passed to all match functions
        matchPending: static (in Status.Pending _, in int count) => $"Queued {count} requests - system pending",
        matchActive: static (in Status.Active _, in int count) => $"Processing {count} requests on active system",
        matchComplete: static (in Status.Complete _, in int count) => $"Cannot process {count} requests - system complete",
        defaultMatch: static (in Status _, in int count) => throw new InvalidOperationException($"Unknown status cannot process {count} requests"));
}