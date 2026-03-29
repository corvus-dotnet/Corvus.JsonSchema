// <copyright file="BuildWithContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

#if NET9_0_OR_GREATER
public readonly ref struct BuildWithContext<TContext, TBuilder>
    where TContext : allows ref struct
#else
public readonly struct BuildWithContext<TContext, TBuilder>
#endif
{
    public BuildWithContext(TContext context, TBuilder build)
    {
        Context = context;
        Build = build;
    }

    public TContext Context { get; }

    public TBuilder Build { get; }
}

public static class BuildWithContext
{
    public static BuildWithContext<TContext, TBuilder> Create<TContext, TBuilder>(scoped in TContext context, TBuilder build)
#if NET9_0_OR_GREATER
    where TContext : allows ref struct
#endif
    {
        return new BuildWithContext<TContext, TBuilder>(context, build);
    }
}