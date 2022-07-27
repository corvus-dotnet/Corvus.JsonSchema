// <copyright file="JsonPatchExtensions.MoveVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// JSON Patch extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private struct MoveVisitor
    {
        public MoveVisitor(string path, string from, in JsonAny sourceElement)
        {
            this.Path = path;
            this.From = from;
            this.TerminatingFromBegin = from.LastIndexOf('/') + 1;
            this.TerminatingPathBegin = path.LastIndexOf('/') + 1;
            this.Added = false;
            this.Removed = false;
            this.Nop = false;
            this.SourceElement = sourceElement;
        }

        public JsonAny SourceElement { get; }

        public string From { get; }

        public bool Nop { get; }

        public int TerminatingFromBegin { get; }

        public string Path { get; }

        public int TerminatingPathBegin { get; }

        public bool Added { get; set; }

        public bool Removed { get; set; }

        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            Walk skipChildren = Walk.SkipChildren;
            result.Output = nodeToVisit;
            Transformed transformed = Transformed.No;

            // We have fallen through because we have either Added already, or we have just added and not yet removed
            if (!this.Removed)
            {
                // Otherwise, this is a remove operation at the source location.
                RemoveVisitor.VisitForRemove(path, nodeToVisit, this.From, this.From[this.TerminatingFromBegin..], ref result);

                if (result.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
                {
                    // We failed, so fail
                    return;
                }

                // We succeeded on the "Remove" part, so say that we have removed
                if (result.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
                {
                    if (this.Added)
                    {
                        return;
                    }

                    this.Removed = true;
                    transformed = Transformed.Yes;
                }

                if (result.Walk != Walk.SkipChildren)
                {
                    // We need to continue down this path
                    skipChildren = Walk.Continue;
                }
            }

            if (!this.Added)
            {
                // Otherwise, this is an add operation with the node we found.
                AddVisitor.VisitForAdd(path, result.Output, this.SourceElement, this.Path, this.Path[this.TerminatingPathBegin..], ref result);
                if (result.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
                {
                    // We failed, so fail
                    result.Output = nodeToVisit;
                    return;
                }

                // We succeeded on the "Add" part, so say that we have added
                if (result.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
                {
                    if (this.Removed)
                    {
                        return;
                    }

                    this.Added = true;
                    transformed = Transformed.Yes;
                }

                if (result.Walk != Walk.SkipChildren)
                {
                    // We need to continue searching down this path
                    skipChildren = Walk.Continue;
                }
            }

            // If we didn't fail out of either added or removed, just continue if either the source or the target
            // are interested in continuing down this path, otherwise skip children, if both are happy to skip
            result.Transformed = transformed;
            result.Walk = skipChildren;
        }
    }
}