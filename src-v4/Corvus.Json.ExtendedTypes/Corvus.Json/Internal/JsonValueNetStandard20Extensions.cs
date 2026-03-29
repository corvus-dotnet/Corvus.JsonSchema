// <copyright file="JsonValueNetStandard20Extensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        return From<TSource, TTarget>(source);
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

    /// <summary>
    /// Create an instance of the given type from a <see cref="JsonElement"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="jsonElement">The <see cref="JsonElement"/> from which to create the instance.</param>
    /// <returns>An instance of the given type backed by the JsonElement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget FromJsonElement<TTarget>(in JsonElement jsonElement)
        where TTarget : struct, IJsonValue<TTarget>
    {
        return From<JsonElement, TTarget>(jsonElement);
    }

    /// <summary>
    /// Create an instance of the default value of the given type.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>An instance of the default value of the given type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget DefaultInstance<TTarget>()
        where TTarget : struct, IJsonValue<TTarget>
    {
        PropertyInfo? prop = typeof(TTarget).GetProperty("DefaultInstance", BindingFlags.Static | BindingFlags.Public);
        if (prop is null)
        {
            return default;
        }

        return (TTarget)prop.GetValue(null);
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
            il.Emit(OpCodes.Ldarga, 0);
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
            il.Emit(OpCodes.Ldarga, 0);
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
            il.Emit(OpCodes.Ldarga, 0);
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
            LocalBuilder loc1 = il.DeclareLocal(typeof(JsonAny));
            il.Emit(OpCodes.Ldarga, 0);
            il.Emit(OpCodes.Call, asAny.GetGetMethod()!);
            il.Emit(OpCodes.Stloc, loc1);
            il.Emit(OpCodes.Ldloca, loc1);
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