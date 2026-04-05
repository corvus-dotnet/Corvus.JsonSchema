// <copyright file="EvalResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// A readonly discriminated union that can hold either a native double or a JsonElement.
/// Used as the return type for the functional evaluator's delegate tree.
/// Arithmetic operations store results as native doubles, avoiding the
/// double → UTF-8 → FixedJsonValueDocument → JsonElement round-trip.
/// </summary>
internal readonly struct EvalResult
{
    private readonly JsonElement _element;
    private readonly double _doubleValue;
    private readonly bool _isDouble;

    private EvalResult(double value)
    {
        _doubleValue = value;
        _isDouble = true;
    }

    private EvalResult(JsonElement element)
    {
        _element = element;
        _isDouble = false;
    }

    public bool IsDouble
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isDouble;
    }

    public JsonElement Element
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _element;
    }

    public JsonValueKind ValueKind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isDouble ? JsonValueKind.Number : _element.ValueKind;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EvalResult FromDouble(double value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EvalResult FromElement(in JsonElement element) => new(element);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetDouble(out double value)
    {
        if (_isDouble)
        {
            value = _doubleValue;
            return true;
        }

        return TryCoerceElementToDouble(_element, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonElement AsElement(JsonWorkspace workspace)
    {
        if (_isDouble)
        {
            return MaterializeDouble(_doubleValue, workspace);
        }

        return _element;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTruthy()
    {
        if (_isDouble)
        {
            return _doubleValue != 0;
        }

        return JsonLogicHelpers.IsTruthy(_element);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNullOrUndefined() => !_isDouble && _element.IsNullOrUndefined();

    private static bool TryCoerceElementToDouble(in JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            value = 1;
            return true;
        }

        if (element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.Null)
        {
            value = 0;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(element, out JsonElement numElem) && numElem.TryGetDouble(out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        value = 0;
        return false;
    }

    private static JsonElement MaterializeDouble(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonLogicHelpers.NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        return JsonLogicHelpers.Zero();
    }
}