// <copyright file="JsonValueExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;
    using System.Text.Json;
    using Corvus.Extensions;

    /// <summary>
    /// Extension methods for <see cref="IJsonValue"/>.
    /// </summary>
    public static class JsonValueExtensions
    {
        private const int MaxStackChars = 1024;
        private static readonly ConcurrentDictionary<ConverterType, object> FactoryCache = new ();

        private delegate TTarget JsonValueConverter<TSource, TTarget>(TSource source)
            where TTarget : struct, IJsonValue;

        /// <summary>
        /// Serialize the entity to a string.
        /// </summary>
        /// <typeparam name="TValue">The type of <see cref="IJsonValue"/>.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A string representation fo the value.</returns>
        public static string Serialize<TValue>(this TValue value)
            where TValue : struct, IJsonValue
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw);
            value.WriteTo(writer);
            writer.Flush();

            int length = Encoding.UTF8.GetMaxCharCount(abw.WrittenCount);
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
            char[]? pooledChars = null;
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly

            Span<char> chars = length <= JsonConstants.StackallocThreshold ?
                stackalloc char[length] :
                (pooledChars = ArrayPool<char>.Shared.Rent(length));

            int count = Encoding.UTF8.GetChars(abw.WrittenSpan, chars);

            Span<char> writtenChars = chars[..count];
#pragma warning disable SA1000 // Keywords should be spaced correctly
            string result = new(writtenChars);
#pragma warning restore SA1000 // Keywords should be spaced correctly

            if (pooledChars != null)
            {
                writtenChars.Clear();
                ArrayPool<char>.Shared.Return(pooledChars);
            }

            return result;
        }

        /// <summary>
        /// Gets a value determining whether the value is valid.
        /// </summary>
        /// <typeparam name="TValue">The type of <see cref="IJsonValue"/>.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <returns><c>True</c> if the value is valid.</returns>
        public static bool IsValid<TValue>(this TValue value)
            where TValue : struct, IJsonValue
        {
            return value.Validate().IsValid;
        }

        /// <summary>
        /// Gets a value indicating whether the instance has properties.
        /// </summary>
        /// <typeparam name="TValue">The type of the instance.</typeparam>
        /// <param name="value">The instance to check for properties.</param>
        /// <returns>True if the object has any proprties.</returns>
        public static bool HasProperties<TValue>(this TValue value)
            where TValue : struct, IJsonObject<TValue>
        {
            return value.EnumerateObject().MoveNext();
        }

        /// <summary>
        /// Gets the item at the given index in the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to get.</typeparam>
        /// <param name="value">The value to get.</param>
        /// <param name="index">The index at which to get the item.</param>
        /// <returns>The item at the index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was outside the bounds of the array.</exception>
        public static TItem GetItem<TItem>(this JsonArray value, int index)
            where TItem : struct, IJsonValue
        {
            int currentIndex = 0;
            JsonArrayEnumerator enumerator = value.EnumerateArray();
            while (enumerator.MoveNext())
            {
                if (currentIndex == index)
                {
                    return enumerator.CurrentAs<TItem>();
                }

                currentIndex++;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Gets the item at the given index in the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to get.</typeparam>
        /// <param name="value">The value to get.</param>
        /// <param name="index">The index at which to get the item.</param>
        /// <returns>The item at the index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was outside the bounds of the array.</exception>
        public static TItem GetItem<TItem>(this JsonAny value, int index)
            where TItem : struct, IJsonValue
        {
            int currentIndex = 0;
            JsonArrayEnumerator enumerator = value.EnumerateArray();
            while (enumerator.MoveNext())
            {
                if (currentIndex == index)
                {
                    return enumerator.CurrentAs<TItem>();
                }

                currentIndex++;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Gets the item at the given index in the array.
        /// </summary>
        /// <typeparam name="TItem">The type of the item to get.</typeparam>
        /// <param name="value">The value to get.</param>
        /// <param name="index">The index at which to get the item.</param>
        /// <returns>The item at the index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was outside the bounds of the array.</exception>
        public static TItem GetItem<TItem>(this JsonNotAny value, int index)
            where TItem : struct, IJsonValue
        {
            int currentIndex = 0;
            JsonArrayEnumerator enumerator = value.EnumerateArray();
            while (enumerator.MoveNext())
            {
                if (currentIndex == index)
                {
                    return enumerator.CurrentAs<TItem>();
                }

                currentIndex++;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Gets the item at the given index in the array.
        /// </summary>
        /// <typeparam name="TArray">The type of the array.</typeparam>
        /// <typeparam name="TItem">The type of the item to get.</typeparam>
        /// <param name="value">The value to get.</param>
        /// <param name="index">The index at which to get the item.</param>
        /// <returns>The item at the index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was outside the bounds of the array.</exception>
        public static TItem GetItem<TArray, TItem>(this TArray value, int index)
            where TArray : struct, IJsonArray<TArray>
            where TItem : struct, IJsonValue
        {
            int currentIndex = 0;
            JsonArrayEnumerator enumerator = value.EnumerateArray();
            while (enumerator.MoveNext())
            {
                if (currentIndex == index)
                {
                    return enumerator.CurrentAs<TItem>();
                }

                currentIndex++;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Convert from the source to the target type.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="source">The value from which to convert.</param>
        /// <returns>An instance of the target type.</returns>
        public static TTarget As<TSource, TTarget>(this TSource source)
            where TSource : struct, IJsonValue
            where TTarget : struct, IJsonValue
        {
            Type targetType = typeof(TTarget);

            if (targetType == typeof(TSource))
            {
                return CastTo<TTarget>.From(source);
            }

            if (targetType == typeof(JsonObject))
            {
                return CastTo<TTarget>.From(source.AsObject());
            }

            if (targetType == typeof(JsonAny))
            {
                return CastTo<TTarget>.From(source.AsAny);
            }

            if (targetType == typeof(JsonArray))
            {
                return CastTo<TTarget>.From(source.AsArray());
            }

            if (targetType == typeof(JsonNumber))
            {
                return CastTo<TTarget>.From(source.AsNumber());
            }

            if (targetType == typeof(JsonString))
            {
                return CastTo<TTarget>.From(source.AsString());
            }

            if (targetType == typeof(JsonBoolean))
            {
                return CastTo<TTarget>.From(source.AsBoolean());
            }

            if (source.HasJsonElement)
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
        /// Gets a value which determines if the value is null or undefined.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>True</c> if the value is Null or Undefined.</returns>
        public static bool IsNullOrUndefined<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind == JsonValueKind.Null || valueKind == JsonValueKind.Undefined;
        }

        /// <summary>
        /// Gets a value which determines if the value is null.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>True</c> if the value is Null.</returns>
        public static bool IsNull<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind == JsonValueKind.Null;
        }

        /// <summary>
        /// Gets a value which determines if the value is not null.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>False</c> if the value is Null.</returns>
        public static bool IsNotNull<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind != JsonValueKind.Null;
        }

        /// <summary>
        /// Gets a value which determines if the value is undefined.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>True</c> if the value is Undefined.</returns>
        public static bool IsUndefined<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind == JsonValueKind.Undefined;
        }

        /// <summary>
        /// Gets a value which determines if the value is not undefined.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>False</c> if the value is Undefined.</returns>
        public static bool IsNotUndefined<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind != JsonValueKind.Undefined;
        }

        /// <summary>
        /// Gets a value which determines if the value is not null or undefined.
        /// </summary>
        /// <typeparam name="T">The type of value to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns><c>False</c> if the value is Null or Undefined.</returns>
        public static bool IsNotNullOrUndefined<T>(this T value)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = value.ValueKind;
            return valueKind != JsonValueKind.Undefined && valueKind != JsonValueKind.Null;
        }

        /// <summary>
        /// Gets the value as a nullable value.
        /// </summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <param name="value">The value to get.</param>
        /// <returns>Returns <c>null</c> if the value is of kind Null or Undefined, otherwise it returns the value.</returns>
        public static T? AsOptional<T>(this T value)
            where T : struct, IJsonValue
        {
            if (value.IsNull() || value.IsUndefined())
            {
                return default;
            }

            return value;
        }

        /// <summary>
        /// Convert to a JsonNumber.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonNumber"/>.</returns>
        public static JsonNumber AsNumber<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonNumber))
            {
                return CastTo<JsonNumber>.From(value);
            }

            return value.AsAny.AsNumber;
        }

        /// <summary>
        /// Convert to a JsonObject.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonObject"/>.</returns>
        public static JsonObject AsObject<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonObject))
            {
                return CastTo<JsonObject>.From(value);
            }

            return value.AsAny.AsObject;
        }

        /// <summary>
        /// Convert to a JsonArray.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonArray"/>.</returns>
        public static JsonArray AsArray<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonArray))
            {
                return CastTo<JsonArray>.From(value);
            }

            return value.AsAny.AsArray;
        }

        /// <summary>
        /// Convert to a JsonString.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonString"/>.</returns>
        public static JsonString AsString<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonString))
            {
                return CastTo<JsonString>.From(value);
            }

            return value.AsAny.AsString;
        }

        /// <summary>
        /// Convert to a JsonBoolean.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonBoolean"/>.</returns>
        public static JsonBoolean AsBoolean<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonBoolean))
            {
                return CastTo<JsonBoolean>.From(value);
            }

            return value.AsAny.AsBoolean;
        }

        /// <summary>
        /// Convert to a JsonNull.
        /// </summary>
        /// <typeparam name="T">The type from which to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="JsonNull"/>.</returns>
        public static JsonNull AsNull<T>(this T value)
            where T : struct, IJsonValue
        {
            if (typeof(T) == typeof(JsonNull))
            {
                return CastTo<JsonNull>.From(value);
            }

            return default;
        }

        /// <summary>
        /// Gets the raw text for the value.
        /// </summary>
        /// <typeparam name="T">The type of the instance from which to get the raw text.</typeparam>
        /// <param name="value">The value for which to get the raw text.</param>
        /// <param name="options">The <see cref="JsonWriterOptions"/>.</param>
        /// <returns>The raw text representation of the value.</returns>
        public static ReadOnlySpan<byte> GetRawText<T>(this T value, JsonWriterOptions options = default)
            where T : struct, IJsonValue
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(abw, options);
            value.WriteTo(writer);
            writer.Flush();
            return abw.WrittenSpan;
        }

        /// <summary>
        /// Force the value to a <see cref="JsonElement"/> backing.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to convert.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value with a JsonElement backing.</returns>
        /// <remarks>This will force a serialization of the element if it did not already have a JsonElement backing.</remarks>
        public static T WithJsonBacking<T>(this T value)
            where T : struct, IJsonValue
        {
            return JsonAny.From(value.AsJsonElement).As<T>();
        }

        /// <summary>
        /// Create an instance of the given type from a <see cref="JsonElement"/>.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="jsonElement">The <see cref="JsonElement"/> from which to create the instance.</param>
        /// <returns>An instance of the given type backed by the JsonElement.</returns>
        internal static TTarget FromJsonElement<TTarget>(in JsonElement jsonElement)
            where TTarget : struct, IJsonValue
        {
            return From<JsonElement, TTarget>(jsonElement);
        }

        private static TTarget FromBoolean<TTarget>(in JsonBoolean jsonBoolean)
            where TTarget : struct, IJsonValue
        {
            return From<JsonBoolean, TTarget>(jsonBoolean);
        }

        private static TTarget FromString<TTarget>(in JsonString jsonString)
            where TTarget : struct, IJsonValue
        {
            return From<JsonString, TTarget>(jsonString);
        }

        private static TTarget FromNumber<TTarget>(in JsonNumber jsonNumber)
            where TTarget : struct, IJsonValue
        {
            return From<JsonNumber, TTarget>(jsonNumber);
        }

        private static TTarget FromObject<TTarget>(in JsonObject jsonObject)
            where TTarget : struct, IJsonValue
        {
            return From<JsonObject, TTarget>(jsonObject);
        }

        private static TTarget FromArray<TTarget>(in JsonArray jsonArray)
            where TTarget : struct, IJsonValue
        {
            return From<JsonArray, TTarget>(jsonArray);
        }

        private static TTarget From<TSource, TTarget>(in TSource source)
            where TTarget : struct, IJsonValue
        {
            JsonValueConverter<TSource, TTarget> func = CastTo<JsonValueConverter<TSource, TTarget>>.From(FactoryCache.GetOrAdd(new ConverterType(typeof(TSource), typeof(TTarget)), BuildConverter<TSource, TTarget>));
            return func(source);
        }

        private static object BuildConverter<TSource, TTarget>(ConverterType arg)
            where TTarget : struct, IJsonValue
        {
            Type sourceType = typeof(TSource);

            Type returnType = typeof(TTarget);
            Type[] argumentTypes = new[] { sourceType };

            ConstructorInfo? ctor = returnType.GetConstructor(argumentTypes);
            if (ctor == null)
            {
                return new JsonValueConverter<TSource, TTarget>(CreateDefault);
            }

            var dynamic = new DynamicMethod(
                $"${returnType.Name}_From{sourceType.Name}",
                returnType,
                argumentTypes,
                returnType);
            ILGenerator il = dynamic.GetILGenerator();

            il.DeclareLocal(returnType);
            il.Emit(OpCodes.Ldarg, 0);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);

            return dynamic.CreateDelegate(typeof(JsonValueConverter<TSource, TTarget>));

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
}
