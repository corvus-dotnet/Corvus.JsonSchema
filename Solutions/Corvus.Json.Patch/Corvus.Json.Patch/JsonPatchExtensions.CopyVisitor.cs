// <copyright file="JsonPatchExtensions.CopyVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Json Patch Extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct CopyVisitor
    {
        public CopyVisitor(Copy patchOperation, JsonAny sourceElement)
        {
            this.Path = patchOperation.Path;
            this.SourceElement = sourceElement;
        }

        public string Path { get; }

        public JsonAny SourceElement { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            // This is an add operation with the node we found.
            AddVisitor.VisitForAdd(path, nodeToVisit, this.SourceElement, this.Path, ref result);
        }
    }
}