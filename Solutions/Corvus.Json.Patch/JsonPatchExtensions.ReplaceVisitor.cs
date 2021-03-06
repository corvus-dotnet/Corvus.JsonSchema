// <copyright file="JsonPatchExtensions.ReplaceVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using System.Runtime.CompilerServices;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// JSON Patch extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct ReplaceVisitor
    {
        public ReplaceVisitor(Replace patchOperation)
        {
            this.Value = patchOperation.Value;
            this.Path = patchOperation.Path;
        }

        public JsonAny Value { get; }

        public string Path { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VisitResult Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit)
        {
            return VisitForReplace(path, nodeToVisit, this.Value, this.Path);
        }

        internal static VisitResult VisitForReplace(ReadOnlySpan<char> path, in JsonAny nodeToVisit, in JsonAny value, ReadOnlySpan<char> operationPath)
        {
            int operationPathLength = operationPath.Length;

            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPathLength == 0 || operationPath.StartsWith(path))
            {
                if (operationPathLength == path.Length)
                {
                    // We are an exact match, so we can just replace this node.
                    return new(value, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                }

                // Otherwise we need to continue, as we are on the path
                return new(nodeToVisit, Transformed.No, Walk.Continue);
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            return new(nodeToVisit, Transformed.No, Walk.SkipChildren);
        }
    }
}
