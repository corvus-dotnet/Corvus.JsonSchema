using JsonSchemaSample.Api;

// Construct patch operations
PatchOperation.CopyOperation copyOp =
    PatchOperation.CopyOperation.Create("/some/path/to/copy", "/some/copy/destination");

PatchOperation.MoveOperation moveOp =
    PatchOperation.MoveOperation.Create("/some/path/to/move", "/some/move/destination");

// Implicit conversion to the discriminated union type
Console.WriteLine(
    ProcessJsonPatch(copyOp));

Console.WriteLine(
    ProcessJsonPatch(moveOp));

// Each operation has a const value associated with its `Op` value
// You would not normally need to use this directly.
Console.WriteLine(PatchOperation.MoveOperation.OpEntity.ConstInstance);
Console.WriteLine(PatchOperation.CopyOperation.OpEntity.ConstInstance);

// The processing function pattern matches on the types in the discriminated union
string ProcessJsonPatch(PatchOperation op)
{
    return op.Match(
        (in PatchOperation.AddOperation add) => $"Add: {add.Path} to {add.Value}",
        (in PatchOperation.RemoveOperation add) => $"Remove: {add.Path}",
        (in PatchOperation.ReplaceOperation add) => $"Replace: {add.Path} with {add.Value}",
        (in PatchOperation.MoveOperation add) => $"Move: {add.FromValue} to {add.Path}",
        (in PatchOperation.CopyOperation add) => $"Copy: {add.FromValue} to {add.Path}",
        (in PatchOperation.TestOperation add) => $"Add: {add.Value} at {add.Path}",
        (in PatchOperation op) => throw new InvalidOperationException($"Unknown JSON patch operation: {op}"));

}