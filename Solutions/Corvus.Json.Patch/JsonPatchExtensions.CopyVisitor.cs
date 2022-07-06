// <copyright file="JsonPatchExtensions.CopyVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using System.Runtime.CompilerServices;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

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
        public VisitResult Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit)
        {
            // This is an add operation with the node we found.
            return AddVisitor.VisitForAdd(path, nodeToVisit, this.SourceElement, this.Path);
        }
    }
}
