//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Model.V3;
public readonly partial struct Basictypes
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct ExtJsonInt16 
#if NET8_0_OR_GREATER
: IJsonNumber<ExtJsonInt16>,
  IAdditionOperators<ExtJsonInt16, ExtJsonInt16, ExtJsonInt16>,
  ISubtractionOperators<ExtJsonInt16, ExtJsonInt16, ExtJsonInt16>,
  IMultiplyOperators<ExtJsonInt16, ExtJsonInt16, ExtJsonInt16>,
  IDivisionOperators<ExtJsonInt16, ExtJsonInt16, ExtJsonInt16>,
  IIncrementOperators<ExtJsonInt16>,
  IDecrementOperators<ExtJsonInt16>
#else
    : IJsonNumber<ExtJsonInt16>
#endif
    {
        /// <summary>
        /// Initializes a new instance of the <see cref = "ExtJsonInt16"/> struct.
        /// </summary>
        /// <param name = "value">The value from which to construct the instance.</param>
        public ExtJsonInt16(in BinaryJsonNumber value)
        {
            this.jsonElementBacking = default;
            this.backing = Backing.Number;
            this.numberBacking = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref = "ExtJsonInt16"/> struct.
        /// </summary>
        /// <param name = "value">The value from which to initialize the number.</param>
        public ExtJsonInt16(short value) : this(new BinaryJsonNumber(value))
        {
        }

        /// <summary>
        /// Conversion from JsonNumber.
        /// </summary>
        /// <param name = "value">The value from which to convert.</param>
        public static implicit operator ExtJsonInt16(JsonNumber value)
        {
            if (value.HasDotnetBacking && value.ValueKind == JsonValueKind.Number)
            {
                return new(value.AsBinaryJsonNumber);
            }

            return new(value.AsJsonElement);
        }

        /// <summary>
        /// Conversion to byte.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as a byte.</exception>
        public static explicit operator byte (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetByte();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<byte>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to decimal.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as a decimal.</exception>
        public static explicit operator decimal (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetDecimal();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<decimal>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to double.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as a double.</exception>
        public static explicit operator double (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetDouble();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<double>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to Int16.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an Int16.</exception>
        public static implicit operator short (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetInt16();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<short>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to Int32.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an Int32.</exception>
        public static explicit operator int (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetInt32();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<int>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to Int64.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an Int64.</exception>
        public static explicit operator long (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetInt64();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<long>();
            }

            throw new InvalidOperationException();
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to Int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an Int64.</exception>
    public static explicit operator Int128(ExtJsonInt16 value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetInt128();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<Int128>();
        }

        throw new InvalidOperationException();
    }
#endif
        /// <summary>
        /// Conversion to SByte.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an SByte.</exception>
        public static explicit operator sbyte (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetSByte();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<sbyte>();
            }

            throw new InvalidOperationException();
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a Single.</exception>
    public static explicit operator Half(ExtJsonInt16 value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetHalf();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<Half>();
        }

        throw new InvalidOperationException();
    }
#endif
        /// <summary>
        /// Conversion to Single.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as a Single.</exception>
        public static explicit operator float (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetSingle();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<float>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to UInt16.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an UInt16.</exception>
        public static explicit operator ushort (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetUInt16();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<ushort>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to UInt32.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an UInt32.</exception>
        public static explicit operator uint (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetUInt32();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<uint>();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Conversion to UInt64.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        /// <exception cref = "InvalidOperationException">The value was not a number.</exception>
        /// <exception cref = "FormatException">The value was not formatted as an UInt64.</exception>
        public static explicit operator ulong (ExtJsonInt16 value)
        {
            if ((value.backing & Backing.JsonElement) != 0)
            {
                return value.jsonElementBacking.SafeGetUInt64();
            }

            if ((value.backing & Backing.Number) != 0)
            {
                return value.numberBacking.CreateChecked<ulong>();
            }

            throw new InvalidOperationException();
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion to UInt128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as an UInt64.</exception>
    public static explicit operator UInt128(ExtJsonInt16 value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.SafeGetUInt128();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking.CreateChecked<UInt128>();
        }

        throw new InvalidOperationException();
    }
#endif
        /// <summary>
        /// Conversion from decimal.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(decimal value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from double.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(double value)
        {
            return new(new BinaryJsonNumber(value));
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Half.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator ExtJsonInt16(Half value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif
        /// <summary>
        /// Conversion from float.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(float value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from byte.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(byte value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from short.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static implicit operator ExtJsonInt16(short value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from int.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(int value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from long.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(long value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from sbyte.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(sbyte value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from ushort.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(ushort value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from uint.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(uint value)
        {
            return new(new BinaryJsonNumber(value));
        }

        /// <summary>
        /// Conversion from ulong.
        /// </summary>
        /// <param name = "value">The value to convert.</param>
        public static explicit operator ExtJsonInt16(ulong value)
        {
            return new(new BinaryJsonNumber(value));
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Int128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator ExtJsonInt16(Int128 value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from UInt128.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator ExtJsonInt16(UInt128 value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif
        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <param name = "left">The LHS of the comparison.</param>
        /// <param name = "right">The RHS of the comparison.</param>
        /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
        public static bool operator <(in ExtJsonInt16 left, in ExtJsonInt16 right)
        {
            return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) < 0;
        }

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <param name = "left">The LHS of the comparison.</param>
        /// <param name = "right">The RHS of the comparison.</param>
        /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
        public static bool operator>(in ExtJsonInt16 left, in ExtJsonInt16 right)
        {
            return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) > 0;
        }

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <param name = "left">The LHS of the comparison.</param>
        /// <param name = "right">The RHS of the comparison.</param>
        /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
        public static bool operator <=(in ExtJsonInt16 left, in ExtJsonInt16 right)
        {
            return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) <= 0;
        }

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <param name = "left">The LHS of the comparison.</param>
        /// <param name = "right">The RHS of the comparison.</param>
        /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
        public static bool operator >=(in ExtJsonInt16 left, in ExtJsonInt16 right)
        {
            return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) >= 0;
        }

        /// <summary>
        /// Adds two values together to compute their sum.
        /// </summary>
        /// <param name = "left">The left hand side.</param>
        /// <param name = "right">The right hand side.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator +(ExtJsonInt16 left, ExtJsonInt16 right)
        {
            return new(left.AsBinaryJsonNumber + right.AsBinaryJsonNumber);
        }

        /// <summary>
        /// Subtracts two values together to compute their difference.
        /// </summary>
        /// <param name = "left">The left hand side.</param>
        /// <param name = "right">The right hand side.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator -(ExtJsonInt16 left, ExtJsonInt16 right)
        {
            return new(left.AsBinaryJsonNumber - right.AsBinaryJsonNumber);
        }

        /// <summary>
        /// Multiplies two values together.
        /// </summary>
        /// <param name = "left">The left hand side.</param>
        /// <param name = "right">The right hand side.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator *(ExtJsonInt16 left, ExtJsonInt16 right)
        {
            return new(left.AsBinaryJsonNumber * right.AsBinaryJsonNumber);
        }

        /// <summary>
        /// Divides two values.
        /// </summary>
        /// <param name = "left">The left hand side.</param>
        /// <param name = "right">The right hand side.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator /(ExtJsonInt16 left, ExtJsonInt16 right)
        {
            return new(left.AsBinaryJsonNumber / right.AsBinaryJsonNumber);
        }

        /// <summary>
        /// Increments the value.
        /// </summary>
        /// <param name = "value">The value.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator ++(ExtJsonInt16 value)
        {
            BinaryJsonNumber num = value.AsBinaryJsonNumber;
            return new(num++);
        }

        /// <summary>
        /// Decrements the value.
        /// </summary>
        /// <param name = "value">The value.</param>
        /// <returns>The resulting value.</returns>
        public static ExtJsonInt16 operator --(ExtJsonInt16 value)
        {
            BinaryJsonNumber num = value.AsBinaryJsonNumber;
            return new(num--);
        }

        /// <summary>
        /// Compare with another number.
        /// </summary>
        /// <param name = "lhs">The lhs of the comparison.</param>
        /// <param name = "rhs">The rhs of the comparison.</param>
        /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
        public static int Compare(in ExtJsonInt16 lhs, in ExtJsonInt16 rhs)
        {
            if (lhs.ValueKind != rhs.ValueKind)
            {
                // We can't be equal if we are not the same underlying type
                return lhs.IsNullOrUndefined() ? 1 : -1;
            }

            if (lhs.IsNull())
            {
                // Nulls are always equal
                return 0;
            }

            if (lhs.backing == Backing.Number && rhs.backing == Backing.Number)
            {
                return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
            }

            // After this point there is no need to check both value kinds because our first quick test verified that they were the same.
            // If either one is a Backing.Number or a JsonValueKind.Number then we know the rhs is compatible.
            if (lhs.backing == Backing.Number && rhs.backing == Backing.Number)
            {
                return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
            }

            if (lhs.backing == Backing.Number && rhs.backing == Backing.JsonElement)
            {
                return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.jsonElementBacking);
            }

            if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.Number)
            {
                return BinaryJsonNumber.Compare(lhs.jsonElementBacking, rhs.numberBacking);
            }

            if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.JsonElement && rhs.jsonElementBacking.ValueKind == JsonValueKind.Number)
            {
                return JsonValueHelpers.NumericCompare(lhs.jsonElementBacking, rhs.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets the value as a short.
        /// </summary>
        /// <returns>The value as a short.</returns>
        public short AsInt16() => (short)this;
        /// <inheritdoc/>
        public BinaryJsonNumber AsBinaryJsonNumber => this.HasDotnetBacking ? this.numberBacking : BinaryJsonNumber.FromJson(this.jsonElementBacking);
    }
}