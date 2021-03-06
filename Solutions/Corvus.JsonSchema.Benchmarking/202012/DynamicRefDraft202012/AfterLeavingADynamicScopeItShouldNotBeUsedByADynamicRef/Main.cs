
    //------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

namespace DynamicRefDraft202012Feature.AfterLeavingADynamicScopeItShouldNotBeUsedByADynamicRef
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Corvus.Json;

        /// <summary>
    /// A type generated from a JsonSchema specification.
    /// </summary>
    public readonly struct Main :
                    IJsonValue,
            IEquatable<Main>
    {

        
    
    
    
    
    
    
    

    
        private readonly JsonElement jsonElementBacking;

    
    
    
            private readonly string? stringBacking;
    
    
        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> struct.
        /// </summary>
        /// <param name="value">The backing <see cref="JsonElement"/>.</param>
        public Main(JsonElement value)
        {
            this.jsonElementBacking = value;
                            this.stringBacking = default;
                }

    
    
    
            /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public Main(string value)
        {
            this.jsonElementBacking = default;
                                            this.stringBacking = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public Main(ReadOnlySpan<char> value)
        {
            this.jsonElementBacking = default;
                                            this.stringBacking = value.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> struct.
        /// </summary>
        /// <param name="value">A string value.</param>
        public Main(ReadOnlySpan<byte> value)
        {
            this.jsonElementBacking = default;
                                            this.stringBacking = System.Text.Encoding.UTF8.GetString(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> struct.
        /// </summary>
        /// <param name="jsonString">The <see cref="JsonString"/> from which to construct the value.</param>
        public Main(JsonString jsonString)
        {
            if (jsonString.HasJsonElement)
            {
                this.jsonElementBacking = jsonString.AsJsonElement;
                this.stringBacking = default;
            }
            else
            {
                this.jsonElementBacking = default;
                this.stringBacking = jsonString;
            }

                                        }
    
    
    
    

    
    
                    /// <summary>
        /// Gets a value indicating whether this matches the If/Then type.
        /// </summary>
        public bool IsIfMatchJsonString
        {
            get
            {
                return this.As<DynamicRefDraft202012Feature.AfterLeavingADynamicScopeItShouldNotBeUsedByADynamicRef.FirstScope>().IsValid(); 
            }
        }

        /// <summary>
        /// Gets this as the matching type for the If/Then clause.
        /// </summary>
        public Corvus.Json.JsonString AsIfMatchJsonString
        {
            get
            {
                return this.As<Corvus.Json.JsonString>(); 
            }
        }

        
            
            /// <summary>
        /// Gets a value indicating whether this is backed by a JSON element.
        /// </summary>
        public bool HasJsonElement =>
    
    
                                this.stringBacking is null
        
                ;

        /// <summary>
        /// Gets the value as a JsonElement.
        /// </summary>
        public JsonElement AsJsonElement
        {
            get
            {
    
    
    
                    if (this.stringBacking is string stringBacking)
                {
                    return JsonString.StringToJsonElement(stringBacking);
                }

    
    
                return this.jsonElementBacking;
            }
        }

        /// <inheritdoc/>
        public JsonValueKind ValueKind
        {
            get
            {
    
    
    
                    if (this.stringBacking is string)
                {
                    return JsonValueKind.String;
                }

    
    
                return this.jsonElementBacking.ValueKind;
            }
        }

        /// <inheritdoc/>
        public JsonAny AsAny
        {
            get
            {
    
    
    
                    if (this.stringBacking is string stringBacking)
                {
                    return new JsonAny(stringBacking);
                }

    
    
                return new JsonAny(this.jsonElementBacking);
            }
        }

    
        
        /// <summary>
        /// Conversion from any.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Main(JsonAny value)
        {
            if (value.HasJsonElement)
            {
                return new Main(value.AsJsonElement);
            }

            return value.As<Main>();
        }

        /// <summary>
        /// Conversion to any.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonAny(Main value)
        {
            return value.AsAny;
        }

    
    
    
        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Main(string value)
        {
            return new Main(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator string(Main value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Main(ReadOnlySpan<char> value)
        {
            return new Main(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<char>(Main value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from utf8 bytes.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Main(ReadOnlySpan<byte> value)
        {
            return new Main(value);
        }

        /// <summary>
        /// Conversion to utf8 bytes.
        /// </summary>
        /// <param name="value">The number from which to convert.</param>
        public static implicit operator ReadOnlySpan<byte>(Main value)
        {
            return value.AsString;
        }

        /// <summary>
        /// Conversion from string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Main(JsonString value)
        {
            return new Main(value);
        }

        /// <summary>
        /// Conversion to string.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonString(Main value)
        {
            return value.AsString;
        }

    
    
    
        /// <summary>
        /// Standard equality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==(Main lhs, Main rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Standard inequality operator.
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison.</param>
        /// <param name="rhs">The right hand side of the comparison.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=(Main lhs, Main rhs)
        {
            return !lhs.Equals(rhs);
        }

    
    
        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is IJsonValue jv)
            {
                return this.Equals(jv.AsAny);
            }

            return obj is null && this.IsNull();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            JsonValueKind valueKind = this.ValueKind;

            return valueKind switch
            {
                    JsonValueKind.Object => this.AsObject().GetHashCode(),
                        JsonValueKind.Array => this.AsArray().GetHashCode(),
                        JsonValueKind.Number => this.AsNumber().GetHashCode(),
                        JsonValueKind.String => this.AsString.GetHashCode(),
                        JsonValueKind.True or JsonValueKind.False => this.AsBoolean().GetHashCode(),
                    JsonValueKind.Null => JsonNull.NullHashCode,
                _ => JsonAny.UndefinedHashCode,
            };
        }

        /// <summary>
        /// Writes the object to the <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the object.</param>
        public void WriteTo(Utf8JsonWriter writer)
        {
    
    
    
                if (this.stringBacking is string stringBacking)
            {
                writer.WriteStringValue(stringBacking);
                return;
            }

    
    
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Undefined)
            {
                this.jsonElementBacking.WriteTo(writer);
                return;
            }

            writer.WriteNullValue();
        }

    
    
    
        /// <inheritdoc/>
        public bool Equals<T>(T other)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = this.ValueKind;

            if (other.ValueKind != valueKind)
            {
                return false;
            }

            return valueKind switch
            {
                    JsonValueKind.Object => this.AsObject().Equals(other.AsObject()),
                        JsonValueKind.Array => this.AsArray().Equals(other.AsArray()),
                        JsonValueKind.Number => this.AsNumber().Equals(other.AsNumber()),
                        JsonValueKind.String => this.AsString.Equals(other.AsString()),
                        JsonValueKind.True or JsonValueKind.False => this.AsBoolean().Equals(other.AsBoolean()),
                    JsonValueKind.Null => true,
                _ => false,
            };
        }

        /// <inheritdoc/>
        public bool Equals(Main other)
        {
            JsonValueKind valueKind = this.ValueKind;

            if (other.ValueKind != valueKind)
            {
                return false;
            }

            return valueKind switch
            {
                                JsonValueKind.Object => this.AsObject().Equals(other.AsObject()),
                        JsonValueKind.Array => this.AsArray().Equals(other.AsArray()),
                        JsonValueKind.Number => this.AsNumber().Equals(other.AsNumber()),
                        JsonValueKind.String => this.AsString.Equals(other.AsString),
                        JsonValueKind.True or JsonValueKind.False => this.AsBoolean().Equals(other.AsBoolean()),
                    JsonValueKind.Null => true,
                _ => false,
            };
        }

    
    
        /// <inheritdoc/>
        public T As<T>()
            where T : struct, IJsonValue
        {
            return this.As<Main, T>();
        }


    
        /// <inheritdoc/>
        public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
        {
            ValidationContext result = validationContext;
            if (level != ValidationLevel.Flag)
            {
                result = result.UsingStack();
            }
        
        

    
    
    
    
    
        
    
                result = this.ValidateIfThenElse(result, level);
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

    
    
    
    
    
    

                return result;
        }

    
    
    
            /// <summary>
        /// Gets the value as a <see cref="JsonString"/>.
        /// </summary>
        private JsonString AsString
        {
            get
            {
                if (this.stringBacking is string stringBacking)
                {
                    return new JsonString(stringBacking);
                }

                return new JsonString(this.jsonElementBacking);
            }
        }
    
    
    
    
    
    
    
    
    
    
            

            

            

            

            
        private ValidationContext ValidateIfThenElse(in ValidationContext validationContext, ValidationLevel level)
        {
            ValidationContext result = validationContext;

            ValidationContext ifResult = this.As<DynamicRefDraft202012Feature.AfterLeavingADynamicScopeItShouldNotBeUsedByADynamicRef.FirstScope>().Validate(validationContext.CreateChildContext(), level);

            if (!ifResult.IsValid)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    result = validationContext.MergeResults(false, level, ifResult, ifResult);
                }
                else if (level >= ValidationLevel.Basic)
                {
                    result = validationContext.MergeResults(false, level, ifResult, ifResult);
                }
            }
            else
            {
                if (level >= ValidationLevel.Basic)
                {
                    result = result.MergeChildContext(ifResult, true);
                }

                result = result.MergeChildContext(ifResult, false);
            }


                
            if (ifResult.IsValid)
            {
                ValidationContext thenResult = this.As<Corvus.Json.JsonString>().Validate(validationContext.CreateChildContext(), level);

                if (!thenResult.IsValid)
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = validationContext.MergeResults(false, level, ifResult, thenResult).WithResult(isValid: false, "Validation 9.2.2.2. then - failed to validate against the then schema.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = validationContext.MergeResults(false, level, ifResult, thenResult).WithResult(isValid: false, "Validation 9.2.2.2. then - failed to validate against the then schema.");
                    }
                    else
                    {
                        result = validationContext.WithResult(isValid: false);
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Basic)
                    {
                        result = result.MergeChildContext(thenResult, true);
                    }

                    result = result.MergeChildContext(thenResult, false);
                }
            }

        
        
            return result;
        }

    
    
    
    
    
    
    }
    }
    