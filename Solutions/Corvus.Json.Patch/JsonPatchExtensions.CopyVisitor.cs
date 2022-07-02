// <copyright file="JsonPatchExtensions.CopyVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// Json Patch Extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private class CopyVisitor
    {
        public CopyVisitor(JsonAny node, Copy patchOperation)
        {
            this.PatchOperation = patchOperation;

            this.SourceElement = FindSourceElement(node, patchOperation.From);
        }

        public Copy PatchOperation { get; }

        public JsonAny? SourceElement { get; }

        public VisitResult Visit(ReadOnlySpan<char> path, JsonAny nodeToVisit)
        {
            // This is an add operation with the node we found.
            return AddVisitor.VisitForAdd(path, nodeToVisit, this.SourceElement, this.PatchOperation.Path);
        }
    }
}
