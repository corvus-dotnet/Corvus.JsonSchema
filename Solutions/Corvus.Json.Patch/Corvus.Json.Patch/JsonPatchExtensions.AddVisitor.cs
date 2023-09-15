// <copyright file="JsonPatchExtensions.AddVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Json Patch implementation details.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct AddVisitor
    {
        public AddVisitor(string path, in JsonAny value)
        {
            this.Path = path;
            this.Value = value;
            this.TerminatingPathIndexBegin = this.Path.LastIndexOf('/') + 1;
        }

        public JsonAny Value { get; }

        public string Path { get; }

        public int TerminatingPathIndexBegin { get; }

        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            ReadOnlySpan<char> span = this.Path.AsSpan();
            VisitForAdd(path, nodeToVisit, this.Value, span, span[this.TerminatingPathIndexBegin..], ref result);
        }

        // This is used by AddVisitor, CopyVistor and MoveVisitor
        internal static void VisitForAdd(ReadOnlySpan<char> path, in JsonAny nodeToVisit, in JsonAny value, ReadOnlySpan<char> operationPath, ReadOnlySpan<char> terminatingPathElement, ref VisitResult result)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    result.Output = value;
                    result.Transformed = Transformed.Yes;
                    result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                    return;
                }
                else if (operationPath[path.Length] != '/')
                {
                    // If our next character is not a path separator, then we must have a partial node match, and we need to skip on to the next sibling.
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.SkipChildren;
                    return;
                }

                VisitForAddCore(path, nodeToVisit, value, operationPath, terminatingPathElement, ref result);
                return;
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.SkipChildren;
            return;
        }

        private static void VisitForAddCore(ReadOnlySpan<char> path, in JsonAny nodeToVisit, in JsonAny value, ReadOnlySpan<char> operationPath, ReadOnlySpan<char> terminatingPathElement, ref VisitResult result)
        {
            if (path.Length + terminatingPathElement.Length + 1 != operationPath.Length)
            {
                // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                result.Output = nodeToVisit;
                result.Transformed = Transformed.No;
                result.Walk = Walk.Continue;
                return;
            }

            if (nodeToVisit.ValueKind == JsonValueKind.Object)
            {
                // Return the transformed result, and stop walking the tree here.
                result.Output = nodeToVisit.AsObject.SetProperty(terminatingPathElement, value);
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                return;
            }

            if (nodeToVisit.ValueKind == JsonValueKind.Array)
            {
                JsonArray array = nodeToVisit.AsArray;
                int arrayLength = array.GetArrayLength();

                if (terminatingPathElement[0] == '-')
                {
                    if (terminatingPathElement.Length == 1)
                    {
                        // We got the '-' which means add it at the end
                        AddNodeAtEnd(array, value, ref result);
                        return;
                    }
                    else
                    {
                        result.Output = nodeToVisit;
                        result.Transformed = Transformed.No;
                        result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                        return;
                    }
                }

                if (TryGetArrayIndex(terminatingPathElement, out int index))
                {
                    // You can specify the end explicitly
                    if (index == arrayLength)
                    {
                        AddNodeAtEnd(array, value, ref result);
                        return;
                    }

                    if (index < arrayLength)
                    {
                        InsertNode(index, array, value, ref result);
                        return;
                    }
                }

                // The index wasn't in the correct form (either because it was past the end, or not in an index format)
                result.Output = nodeToVisit;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }

            // The parent entity wasn't an object or an array, so it can't be added to; this is an error.
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
        }

        private static void AddNodeAtEnd(in JsonArray arrayNode, in JsonAny node, ref VisitResult result)
        {
            result.Output = arrayNode.Add(node);
            result.Transformed = Transformed.Yes;
            result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
        }

        private static void InsertNode(int index, in JsonArray arrayNode, in JsonAny node, ref VisitResult result)
        {
            result.Output = arrayNode.Insert(index, node);
            result.Transformed = Transformed.Yes;
            result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
        }
    }
}