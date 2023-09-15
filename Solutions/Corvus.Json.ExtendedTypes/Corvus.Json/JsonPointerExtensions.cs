// <copyright file="JsonPointerExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Utility function to resolve the JsonElement referenced by a json pointer into a json element.
/// </summary>
/// <remarks>
/// Note that we don't support <c>$anchor</c> or <c>$id</c> with this implementation.
/// </remarks>
public static class JsonPointerExtensions
{
    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="pointer">The pointer to resolve.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static JsonAny ResolvePointer<T>(this T root, in JsonPointer pointer)
        where T : struct, IJsonValue
    {
        if (ResolvePointerInternal(root, pointer.AsSpan(), true, out JsonAny element))
        {
            return element;
        }

        throw new InvalidOperationException("Internal error: ResolvePointerInternal() should have thrown if it failed to resolve a fragment");
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="pointer">The pointer to resolve.</param>
    /// <param name="element">The element found at the given location.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static bool TryResolvePointer<T>(this T root, in JsonPointer pointer, out JsonAny element)
        where T : struct, IJsonValue
    {
        return ResolvePointerInternal(root, pointer.AsSpan(), false, out element);
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="pointer">The pointer to resolve.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static JsonAny ResolvePointer<T>(this T root, in JsonRelativePointer pointer)
        where T : struct, IJsonValue
    {
        if (ResolvePointerInternal(root, pointer.AsSpan(), true, out JsonAny element))
        {
            return element;
        }

        throw new InvalidOperationException("Internal error: ResolvePointerInternal() should have thrown if it failed to resolve a fragment");
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="pointer">The pointer to resolve.</param>
    /// <param name="element">The element found at the given location.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static bool TryResolvePointer<T>(this T root, in JsonRelativePointer pointer, out JsonAny element)
        where T : struct, IJsonValue
    {
        return ResolvePointerInternal(root, pointer.AsSpan(), false, out element);
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="fragment">The fragment in <c>#/blah/foo/3/bar/baz</c> form.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static JsonAny ResolvePointer<T>(this T root, ReadOnlySpan<char> fragment)
        where T : struct, IJsonValue
    {
        if (ResolvePointerInternal(root, fragment, true, out JsonAny element))
        {
            return element;
        }

        throw new InvalidOperationException("Internal error: ResolvePointerInternal() should have thrown if it failed to resolve a fragment");
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to which to apply the pointer.</typeparam>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="fragment">The fragment in <c>#/blah/foo/3/bar/baz</c> form.</param>
    /// <param name="element">The element found at the given location.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    public static bool TryResolvePointer<T>(this T root, ReadOnlySpan<char> fragment, out JsonAny element)
        where T : struct, IJsonValue
    {
        return ResolvePointerInternal(root, fragment, false, out element);
    }

    /// <summary>
    /// Decodes the ~ encoding in a reference.
    /// </summary>
    /// <param name="encodedFragment">The encoded reference.</param>
    /// <param name="fragment">The span into which to write the result.</param>
    /// <returns>The length of the decoded reference.</returns>
    public static int DecodePointer(ReadOnlySpan<char> encodedFragment, Span<char> fragment)
    {
        int readIndex = 0;
        int writeIndex = 0;

        while (readIndex < encodedFragment.Length)
        {
            if (encodedFragment[readIndex] != '~')
            {
                fragment[writeIndex] = encodedFragment[readIndex];
                readIndex++;
                writeIndex++;
            }
            else
            {
                if (readIndex >= encodedFragment.Length - 1)
                {
                    throw new JsonException($"Expected to find 0, 1 or 2 after '~' in the component {encodedFragment.ToString()} at index {readIndex}, but found the end of the component.");
                }

                if (encodedFragment[readIndex + 1] == '0')
                {
                    fragment[writeIndex] = '~';
                }
                else if (encodedFragment[readIndex + 1] == '1')
                {
                    fragment[writeIndex] = '/';
                }
                else
                {
                    throw new JsonException($"Expected to find 0, or 2 after '~' in the component {encodedFragment.ToString()} at index {readIndex}, but found {encodedFragment[readIndex + 1]}");
                }

                readIndex += 2;
                writeIndex++;
            }
        }

        return writeIndex;
    }

    /// <summary>
    /// Resolve a json element from a fragment pointer into a json document.
    /// </summary>
    /// <param name="root">The root element from which to start resolving the pointer.</param>
    /// <param name="fragment">The fragment in <c>#/blah/foo/3/bar/baz</c> form.</param>
    /// <param name="throwOnFailure">If true, we throw on failure.</param>
    /// <param name="element">The element found at the given location.</param>
    /// <returns><c>true</c> if the element was found.</returns>
    private static bool ResolvePointerInternal<T>(T root, ReadOnlySpan<char> fragment, bool throwOnFailure, out JsonAny element)
        where T : struct, IJsonValue
    {
        JsonAny current = root.AsAny;

        int index = 0;
        int startRun = 0;

        char[]? decodedComponentChars = null;

        int length = fragment.Length;
        Span<char> decodedComponent = length <= JsonConstants.StackallocThreshold ?
            stackalloc char[length] :
            (decodedComponentChars = ArrayPool<char>.Shared.Rent(length));

        try
        {
            while (index < fragment.Length)
            {
                if (index == 0 && fragment[index] == '#')
                {
                    ++index;
                }

                if (fragment[index] == '/')
                {
                    ++index;
                }

                startRun = index;

                while (index < fragment.Length && fragment[index] != '/')
                {
                    ++index;
                }

                // We've either reached the fragment.Length (so have to go 1 back from the end)
                // or we're sitting on the terminating '/'
                int endRun = index;
                ReadOnlySpan<char> encodedComponent = fragment[startRun..endRun];
                int decodedWritten = DecodePointer(encodedComponent, decodedComponent);
                ReadOnlySpan<char> component = decodedComponent[..decodedWritten];
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (current.AsObject.TryGetProperty(component, out JsonAny next))
                    {
                        current = next;
                    }
                    else
                    {
                        // We were unable to find the element at that location.
                        if (throwOnFailure)
                        {
                            throw new JsonException($"Unable to find the element at path {fragment[0..endRun].ToString()}.");
                        }
                        else
                        {
                            element = default;
                            return false;
                        }
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    if (component.Length > 1 && component[0] == '0')
                    {
                        // We were unable to find the element at that index in the array.
                        if (throwOnFailure)
                        {
                            throw new JsonException($"Unable to find the array element at path {fragment[0..endRun].ToString()}.");
                        }
                        else
                        {
                            element = default;
                            return false;
                        }
                    }

                    if (int.TryParse(component, out int targetArrayIndex))
                    {
                        int arrayIndex = 0;
                        JsonArrayEnumerator enumerator = current.AsArray.EnumerateArray();
                        while (enumerator.MoveNext() && arrayIndex < targetArrayIndex)
                        {
                            arrayIndex++;
                        }

                        // Check to see if we reached the target, and didn't go off the end of the enumeration.
                        if (arrayIndex == targetArrayIndex && enumerator.Current.ValueKind != JsonValueKind.Undefined)
                        {
                            current = enumerator.Current;
                        }
                        else
                        {
                            // We were unable to find the element at that index in the array.
                            if (throwOnFailure)
                            {
                                throw new JsonException($"Unable to find the array element at path {fragment[0..endRun].ToString()}.");
                            }
                            else
                            {
                                element = default;
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // We couldn't parse the integer of the index
                        if (throwOnFailure)
                        {
                            throw new JsonException($"Expected to find an integer array index at path {fragment[0..endRun].ToString()}, but found {fragment[startRun..endRun].ToString()}.");
                        }
                        else
                        {
                            element = default;
                            return false;
                        }
                    }
                }
            }

            element = current;
            return true;
        }
        finally
        {
            if (decodedComponentChars is char[] dcc)
            {
                ArrayPool<char>.Shared.Return(dcc, true);
            }
        }
    }
}