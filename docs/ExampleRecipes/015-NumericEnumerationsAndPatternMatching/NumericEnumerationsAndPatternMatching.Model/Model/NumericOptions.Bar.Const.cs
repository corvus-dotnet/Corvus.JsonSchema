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
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace JsonSchemaSample.Api;
public readonly partial struct NumericOptions
{
    /// <summary>
    /// The bar item.
    /// </summary>
    public readonly partial struct Bar
    {
        private static readonly Bar __CorvusConstValue = Bar.ParseValue("4"u8);
        /// <summary>
        /// Initializes a new instance of the <see cref = "Bar"/> struct.
        /// </summary>
        public Bar()
        {
            this.jsonElementBacking = __CorvusConstValue.jsonElementBacking;
            this.numberBacking = __CorvusConstValue.numberBacking;
            this.backing = __CorvusConstValue.backing;
        }

        /// <summary>
        /// Gets the constant value for this instance
        /// </summary>
        public static Bar ConstInstance => __CorvusConstValue;
    }
}