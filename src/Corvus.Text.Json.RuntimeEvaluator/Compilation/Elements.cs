// <copyright file="Elements.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// Access to the document/index behind an element without reflection.
/// </summary>
internal static class Elements
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Create<T>(IJsonDocument document, int index)
        where T : struct, IJsonElement<T>
    {
        return T.CreateInstance(document, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IJsonDocument Document<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return element.ParentDocument;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return element.ParentDocumentIndex;
    }
}
