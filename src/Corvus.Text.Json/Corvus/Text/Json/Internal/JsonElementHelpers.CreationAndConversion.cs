// <copyright file="JsonElementHelpers.CreationAndConversion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#if !NET
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for creating and converting JSON element instances using dynamic method generation.
/// </summary>
public static partial class JsonElementHelpers
{
    // Creation delegate
    private delegate T CreateJsonElementInstance<T>(IJsonDocument document, int index)
        where T : struct, IJsonElement<T>;

    private static readonly ConcurrentDictionary<Type, object> Creators = [];

    /// <summary>
    /// Creates an instance of the specified JSON element type using dynamic method generation.
    /// </summary>
    /// <typeparam name="T">The type of JSON element to create, which must implement <see cref="IJsonElement{T}"/>.</typeparam>
    /// <param name="parentDocument">The parent JSON document that contains the element.</param>
    /// <param name="parentDocumentIndex">The index of the element within the parent document.</param>
    /// <returns>A new instance of the specified JSON element type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type does not have a constructor with the required signature.</exception>
    [CLSCompliant(false)]
    public static T CreateInstance<T>(IJsonDocument parentDocument, int parentDocumentIndex)
        where T : struct, IJsonElement<T>
    {
        var creator = (CreateJsonElementInstance<T>)Creators.GetOrAdd(typeof(T), BuildCreator);
        return creator(parentDocument, parentDocumentIndex);

        static CreateJsonElementInstance<T> BuildCreator(Type type)
        {
            Type[] parameters = [typeof(IJsonDocument), typeof(int)];
            ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null)
                ??  throw new InvalidOperationException(SR.Format(SR.TypeDoesNotHaveAConstructorWithTheRequiredSignature, type));

            var dynamic = new DynamicMethod(
                $"Corvus.Text.Json.IJsonElement.Create_{type.FullName}",
                typeof(T),
                parameters,
                true);

            ILGenerator il = dynamic.GetILGenerator();

            // Emit code to call the method
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            return (CreateJsonElementInstance<T>)dynamic.CreateDelegate(typeof(CreateJsonElementInstance<T>));
        }
    }
}
#endif