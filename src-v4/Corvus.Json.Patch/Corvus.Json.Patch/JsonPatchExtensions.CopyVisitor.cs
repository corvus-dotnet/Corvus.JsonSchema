// <copyright file="JsonPatchExtensions.CopyVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Json Patch Extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct CopyVisitor
    {
        public CopyVisitor(string path, in JsonAny sourceElement)
        {
            this.Path = path;
            this.TerminatingPathElementBegin = this.Path.LastIndexOf('/') + 1;
            this.SourceElement = sourceElement;
        }

        public string Path { get; }

        public int TerminatingPathElementBegin { get; }

        public JsonAny SourceElement { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            // This is an add operation with the node we found.
            ReadOnlySpan<char> span = this.Path.AsSpan();
            AddVisitor.VisitForAdd(path, nodeToVisit, this.SourceElement, span, span[this.TerminatingPathElementBegin..], ref result);
        }
    }
}