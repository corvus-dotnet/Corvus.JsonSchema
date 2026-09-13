// <copyright file="Elements.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

#if STJ
namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// Access to the document/index behind an element in the generator build (see <see cref="ParsedJsonDocument{T}"/>).
/// </summary>
internal static class Elements
{
    public static T Create<T>(IJsonDocument document, int index)
        where T : struct
    {
        return (T)(object)document.GetElement(index);
    }

    public static IJsonDocument Document(in JsonElement element)
    {
        return element.ParentDocument;
    }

    public static int Index(in JsonElement element)
    {
        return element.ParentDocumentIndex;
    }
}
#else
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
#if NET8_0_OR_GREATER
        return T.CreateInstance(document, index);
#else
        return JsonElementHelpers.CreateInstance<T>(document, index);
#endif
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
#endif