// <copyright file="JsonWorkspaceCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Defines a thread-local cache for us to store reusable JsonWorkspace instances.
/// </summary>
internal static class JsonWorkspaceCache
{
    [ThreadStatic]
    private static ThreadLocalState? t_threadLocalState;

    /// <summary>
    /// Rents a workspace from the thread-local cache or creates a new one.
    /// </summary>
    /// <param name="initialDocumentCapacity">The initial document capacity for the workspace.</param>
    /// <param name="options">The JSON writer options to use.</param>
    /// <returns>A workspace instance from the cache or a new instance.</returns>
    public static JsonWorkspace RentWorkspace(int initialDocumentCapacity = 5, JsonWriterOptions? options = null)
    {
        ThreadLocalState state = t_threadLocalState ??= new();
        JsonWorkspace workspace;

        if (state.RentedWorkspaces++ == 0)
        {
            // First call in the stack -- initialize & return the cached instance.
            workspace = state.Workspace;
            workspace.Reset(initialDocumentCapacity, options);
        }
        else
        {
            // We've created a second workspace, so we're going to create another instance.
            workspace = new JsonWorkspace(true, initialDocumentCapacity, options);
        }

        return workspace;
    }

    /// <summary>
    /// Returns a workspace to the thread-local cache for reuse.
    /// </summary>
    /// <param name="workspace">The workspace to return to the cache.</param>
    public static void ReturnWorkspace(JsonWorkspace workspace)
    {
        Debug.Assert(t_threadLocalState != null);
        ThreadLocalState state = t_threadLocalState;

        workspace.ResetAllStateForCacheReuse();

        int rentedWorkspaces = --state.RentedWorkspaces;
        Debug.Assert((rentedWorkspaces == 0) == ReferenceEquals(state.Workspace, workspace));
    }

    private sealed class ThreadLocalState
    {
        public readonly JsonWorkspace Workspace;

        public int RentedWorkspaces;

        public ThreadLocalState()
        {
            Workspace = JsonWorkspace.CreateEmptyInstanceForCaching();
        }
    }
}