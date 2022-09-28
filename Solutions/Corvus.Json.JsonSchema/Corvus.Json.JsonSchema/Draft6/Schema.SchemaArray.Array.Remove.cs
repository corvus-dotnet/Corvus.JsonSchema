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

namespace Corvus.Json.JsonSchema.Draft6;
public readonly partial struct Schema
{
    /// <summary>
    /// A type generated from a JsonSchema specification.
    /// </summary>
    public readonly partial struct SchemaArray
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SchemaArray Remove(in JsonAny item1)
        {
            return new(this.GetImmutableListWithout(item1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SchemaArray RemoveAt(int index)
        {
            return new(this.GetImmutableListWithoutRange(index, 1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SchemaArray RemoveRange(int index, int count)
        {
            return new(this.GetImmutableListWithoutRange(index, count));
        }
    }
}