// <copyright file="JsonValueNetStandard20Extensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Extension methods for <see cref="IJsonValue"/>.
/// </summary>
public static class JsonValueNetStandard20Extensions
{
    private static readonly ConcurrentDictionary<ConverterType, object> FactoryCache = new();

    private delegate TTarget JsonValueConverter<TSource, TTarget>(TSource source)
        where TTarget : struct, IJsonValue;

    /// <summary>
    /// Convert from the source to the target type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="source">The value from which to convert.</param>
    /// <returns>An instance of the target type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget As<TSource, TTarget>(this TSource source)
        where TSource : struct, IJsonValue
        where TTarget : struct, IJsonValue
    {
        if (source.HasJsonElementBacking)
        {
            return FromJsonElement<TTarget>(source.AsJsonElement);
        }

        return source.ValueKind switch
        {
            JsonValueKind.Array => FromArray<TTarget>(source.AsArray()),
            JsonValueKind.Object => FromObject<TTarget>(source.AsObject()),
            JsonValueKind.Number => FromNumber<TTarget>(source.AsNumber()),
            JsonValueKind.String => FromString<TTarget>(source.AsString()),
            JsonValueKind.False or JsonValueKind.True => FromBoolean<TTarget>(source.AsBoolean()),
            _ => default,
        };
    }

    /// <summary>
    /// Convert to a JsonNumber.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonNumber"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonNumber AsNumber<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.AsAny.AsNumber;
    }

    /// <summary>
    /// Convert to a JsonObject.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonObject"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonObject AsObject<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.AsAny.AsObject;
    }

    /// <summary>
    /// Convert to a JsonArray.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonArray"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonArray AsArray<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.AsAny.AsArray;
    }

    /// <summary>
    /// Convert to a JsonString.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonString"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonString AsString<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.AsAny.AsString;
    }

    /// <summary>
    /// Convert to a JsonBoolean.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonBoolean"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonBoolean AsBoolean<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.AsAny.AsBoolean;
    }

    /// <summary>
    /// Convert to a JsonNull.
    /// </summary>
    /// <typeparam name="T">The type from which to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="JsonNull"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonNull AsNull<T>(this T value)
        where T : struct, IJsonValue
    {
        return default;
    }

    /// <summary>
    /// Create an instance of the given type from a <see cref="JsonElement"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="jsonElement">The <see cref="JsonElement"/> from which to create the instance.</param>
    /// <returns>An instance of the given type backed by the JsonElement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget FromJsonElement<TTarget>(in JsonElement jsonElement)
        where TTarget : struct, IJsonValue
    {
        return From<JsonElement, TTarget>(jsonElement);
    }

    /// <summary>
    /// Create an instance of the given type from a <see cref="ImmutableList{JsonAny}"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="list">The <see cref="ImmutableList{JsonAny}"/> from which to create the instance.</param>
    /// <returns>An instance of the given type backed by the JsonElement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget From<TTarget>(in ImmutableList<JsonAny> list)
        where TTarget : struct, IJsonArray<TTarget>
    {
        return From<ImmutableList<JsonAny>, TTarget>(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget FromBoolean<TTarget>(in JsonBoolean jsonBoolean)
        where TTarget : struct, IJsonValue
    {
        return From<JsonBoolean, TTarget>(jsonBoolean);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget FromString<TTarget>(in JsonString jsonString)
        where TTarget : struct, IJsonValue
    {
        return From<JsonString, TTarget>(jsonString);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget FromNumber<TTarget>(in JsonNumber jsonNumber)
        where TTarget : struct, IJsonValue
    {
        return From<JsonNumber, TTarget>(jsonNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget FromObject<TTarget>(in JsonObject jsonObject)
        where TTarget : struct, IJsonValue
    {
        return From<JsonObject, TTarget>(jsonObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget FromArray<TTarget>(in JsonArray jsonArray)
        where TTarget : struct, IJsonValue
    {
        return From<JsonArray, TTarget>(jsonArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTarget From<TSource, TTarget>(in TSource source)
        where TTarget : struct, IJsonValue
    {
        var func = (JsonValueConverter<TSource, TTarget>)FactoryCache.GetOrAdd(new ConverterType(typeof(TSource), typeof(TTarget)), BuildConverter<TSource, TTarget>);
        return func(source);
    }

    private static object BuildConverter<TSource, TTarget>(ConverterType arg)
        where TTarget : struct, IJsonValue
    {
        Type sourceType = arg.Source;

        Type returnType = arg.Target;
        Type[] argumentTypes = [sourceType];

        if (typeof(TSource) == typeof(ImmutableList<JsonAny>))
        {
            MethodInfo? fromJson = returnType.GetMethod("From", [typeof(TSource)]);
            if (fromJson == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            var dynamic = new DynamicMethod(
                $"${returnType.Name}_From{sourceType.Name}",
                returnType,
                argumentTypes,
                returnType);

            ILGenerator il = dynamic.GetILGenerator();

            // Emit code to call the From() static method on the targetType using the value provided.
            il.DeclareLocal(returnType);
            il.Emit(OpCodes.Ldarg, 0);
            il.Emit(OpCodes.Call, fromJson);
            il.Emit(OpCodes.Ret);

            return dynamic.CreateDelegate(typeof(JsonValueConverter<TSource, TTarget>));
        }
        else if (typeof(TSource) == typeof(JsonElement))
        {
            MethodInfo? fromJson = returnType.GetMethod("FromJson", BindingFlags.Static | BindingFlags.Public);
            if (fromJson == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            var dynamic = new DynamicMethod(
                $"${returnType.Name}_From{sourceType.Name}",
                returnType,
                argumentTypes,
                returnType);
            ILGenerator il = dynamic.GetILGenerator();

            // Emit code to call the FromJson static method on the targetType using the value provided.
            il.DeclareLocal(returnType);
            il.Emit(OpCodes.Ldarg, 0);
            il.Emit(OpCodes.Call, fromJson);
            il.Emit(OpCodes.Ret);

            return dynamic.CreateDelegate(typeof(JsonValueConverter<TSource, TTarget>));
        }
        else if (typeof(TSource) == typeof(JsonAny))
        {
            MethodInfo fromAny = returnType.GetMethod("FromAny", BindingFlags.Public | BindingFlags.Static);
            if (fromAny == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            var dynamic = new DynamicMethod(
                $"${returnType.Name}_From{sourceType.Name}",
                returnType,
                argumentTypes,
                returnType);

            ILGenerator il = dynamic.GetILGenerator();

            // Emit code to call the fromAny static method on the targetType using the value provided.
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, fromAny);
            il.Emit(OpCodes.Ret);

            return dynamic.CreateDelegate(typeof(JsonValueConverter<TSource, TTarget>));
        }
        else
        {
            MethodInfo fromAny = returnType.GetMethod("FromAny", BindingFlags.Public | BindingFlags.Static);
            if (fromAny == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            PropertyInfo asAny = sourceType.GetProperty("AsAny", BindingFlags.Public | BindingFlags.Instance);
            if (asAny == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            var dynamic = new DynamicMethod(
                $"${returnType.Name}_From{sourceType.Name}",
                returnType,
                argumentTypes,
                returnType);

            ILGenerator il = dynamic.GetILGenerator();

            // Emit code to call the fromAny static method on the targetType using the value returned by the
            // asAny method on the sourceType.
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, asAny.GetGetMethod());
            il.Emit(OpCodes.Call, fromAny);
            il.Emit(OpCodes.Ret);

            return dynamic.CreateDelegate(typeof(JsonValueConverter<TSource, TTarget>));
        }

        static TTarget CreateDefault(TSource source)
        {
            return default;
        }
    }

    private readonly struct ConverterType : IEquatable<ConverterType>
    {
        public ConverterType(Type source, Type target)
        {
            this.Source = source;
            this.Target = target;
        }

        public Type Source { get; }

        public Type Target { get; }

        public static bool operator ==(ConverterType left, ConverterType right)
        {
            return EqualityComparer<ConverterType>.Default.Equals(left, right);
        }

        public static bool operator !=(ConverterType left, ConverterType right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConverterType ct && this.Equals(ct);
        }

        public bool Equals(ConverterType other)
        {
            return EqualityComparer<Type>.Default.Equals(this.Source, other.Source) &&
                   EqualityComparer<Type>.Default.Equals(this.Target, other.Target);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Source, this.Target);
        }
    }
}

#endif