//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Runtime.CompilerServices;
using Corvus.Json;

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    public readonly partial struct Schema
    {
        /// <summary>
        /// Generated from JSON Schema.
        /// </summary>
        public readonly partial struct AnyOfEntityArray
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AnyOfEntityArray IJsonArray<AnyOfEntityArray>.Remove(in JsonAny item1)
            {
                return new(this.GetImmutableListWithout(item1));
            }

            /// <summary>
            /// Remove the specified item from the array.
            /// </summary>
            /// <param name = "item">The item to remove.</param>
            /// <returns>An instance of the array with the item removed.</returns>
            /// <exception cref = "InvalidOperationException">The value was not an array.</exception>
            public AnyOfEntityArray Remove(in Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Schema.AnyOfEntityArray.AnyOfEntity item)
            {
                return new(this.GetImmutableListWithout(item.AsAny));
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AnyOfEntityArray RemoveAt(int index)
            {
                return new(this.GetImmutableListWithoutRange(index, 1));
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AnyOfEntityArray RemoveRange(int index, int count)
            {
                return new(this.GetImmutableListWithoutRange(index, count));
            }
        }
    }
}