// <copyright file="JsonPatchExtensions.MoveVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// JSON Patch extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private struct MoveVisitor
    {
        public MoveVisitor(Move patchOperation, JsonAny sourceElement)
        {
            this.From = patchOperation.From;
            this.Path = patchOperation.Path;
            this.Added = false;
            this.Removed = false;
            this.SourceElement = sourceElement;
        }

        public JsonAny SourceElement { get; }

        public string From { get; }

        public string Path { get; }

        public bool Added { get; set; }

        public bool Removed { get; set; }

        public VisitResult Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit)
        {
            Walk skipChildren = Walk.SkipChildren;

            JsonAny currentNode = nodeToVisit;
            Transformed transformed = Transformed.No;

            // We have fallen through because we have either Added already, or we have just added and not yet removed
            if (!this.Removed)
            {
                // Otherwise, this is a remove operation at the source location.
                VisitResult resultFromRemove = RemoveVisitor.VisitForRemove(path, nodeToVisit, this.From);
                Walk resultFromRemoveWalk = resultFromRemove.Walk;

                if (resultFromRemoveWalk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
                {
                    // We failed, so fail
                    return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                }

                // We succeeded on the "Remove" part, so say that we have removed
                if (resultFromRemoveWalk == Walk.TerminateAtThisNodeAndKeepChanges)
                {
                    this.Removed = true;
                    if (this.Added)
                    {
                        return resultFromRemove;
                    }

                    currentNode = resultFromRemove.Output;
                    transformed = Transformed.Yes;
                }

                if (resultFromRemoveWalk != Walk.SkipChildren)
                {
                    // We need to continue down this path
                    skipChildren = Walk.Continue;
                }
            }

            if (!this.Added)
            {
                // Otherwise, this is an add operation with the node we found.
                VisitResult resultFromAdd = AddVisitor.VisitForAdd(path, currentNode, this.SourceElement, this.Path);
                Walk resultFromAddWalk = resultFromAdd.Walk;
                if (resultFromAddWalk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
                {
                    // We failed, so fail
                    return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                }

                // We succeeded on the "Add" part, so say that we have added
                if (resultFromAddWalk == Walk.TerminateAtThisNodeAndKeepChanges)
                {
                    this.Added = true;
                    if (this.Removed)
                    {
                        return resultFromAdd;
                    }

                    currentNode = resultFromAdd.Output;
                    transformed = Transformed.Yes;
                }

                if (resultFromAddWalk != Walk.SkipChildren)
                {
                    // We need to continue searching down this path
                    skipChildren = Walk.Continue;
                }
            }

            // If we didn't fail out of either added or removed, just continue if either the source or the target
            // are interested in continuing down this path, otherwise skip children, if both are happy to skip
            return new(currentNode, transformed, skipChildren);
        }
    }
}
