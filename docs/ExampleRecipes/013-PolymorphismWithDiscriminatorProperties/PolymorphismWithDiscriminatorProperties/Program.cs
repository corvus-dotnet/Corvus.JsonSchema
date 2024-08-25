﻿using JsonSchemaSample.Api;

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
        (in PatchOperation.AddOperation op) => $"Add: {op.Path} to {op.Value}",
        (in PatchOperation.RemoveOperation op) => $"Remove: {op.Path}",
        (in PatchOperation.ReplaceOperation op) => $"Replace: {op.Path} with {op.Value}",
        (in PatchOperation.MoveOperation op) => $"Move: {op.FromValue} to {op.Path}",
        (in PatchOperation.CopyOperation op) => $"Copy: {op.FromValue} to {op.Path}",
        (in PatchOperation.TestOperation op) => $"Test: {op.Value} at {op.Path}",
        (in PatchOperation op) => throw new InvalidOperationException($"Unknown JSON patch operation: {op}"));

}