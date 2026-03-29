// <copyright file="JsonDocument.StackRowStack.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

// We need to target netstandard2.0, so keep using ref for MemoryMarshal.Write
// CS9191: The 'ref' modifier for argument 2 corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
#pragma warning disable CS9191

namespace Corvus.Text.Json.Internal;

public abstract partial class JsonDocument
{
    /// <summary>
    /// A stack data structure for managing StackRow elements using a rented buffer for memory efficiency.
    /// </summary>
    internal struct StackRowStack : IDisposable
    {
        private byte[] _rentedBuffer;

        private int _topOfStack;

        /// <summary>
        /// Initializes a new instance of the <see cref="StackRowStack"/> struct with the specified initial size.
        /// </summary>
        /// <param name="initialSize">The initial size of the rented buffer.</param>
        public StackRowStack(int initialSize)
        {
            _rentedBuffer = ArrayPool<byte>.Shared.Rent(initialSize);
            _topOfStack = _rentedBuffer.Length;
        }

        /// <summary>
        /// Releases resources used by the stack, returning the rented buffer to the array pool.
        /// </summary>
        public void Dispose()
        {
            byte[] toReturn = _rentedBuffer;
            _rentedBuffer = null!;
            _topOfStack = 0;

            if (toReturn != null)
            {
                // The data in this rented buffer only conveys the positions and
                // lengths of tokens in a document, but no content; so it does not
                // need to be cleared.
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

        /// <summary>
        /// Pushes a StackRow onto the top of the stack, enlarging the buffer if necessary.
        /// </summary>
        /// <param name="row">The StackRow to push onto the stack.</param>
        internal void Push(StackRow row)
        {
            if (_topOfStack < StackRow.Size)
            {
                Enlarge();
            }

            _topOfStack -= StackRow.Size;
            MemoryMarshal.Write(_rentedBuffer.AsSpan(_topOfStack), ref row);
        }

        /// <summary>
        /// Pops and returns the StackRow from the top of the stack.
        /// </summary>
        /// <returns>The StackRow that was removed from the top of the stack.</returns>
        internal StackRow Pop()
        {
            Debug.Assert(_rentedBuffer != null);
            Debug.Assert(_topOfStack <= _rentedBuffer!.Length - StackRow.Size);

            StackRow row = MemoryMarshal.Read<StackRow>(_rentedBuffer.AsSpan(_topOfStack));
            _topOfStack += StackRow.Size;
            return row;
        }

        /// <summary>
        /// Enlarges the rented buffer by doubling its size and copying existing data.
        /// </summary>
        private void Enlarge()
        {
            byte[] toReturn = _rentedBuffer;
            _rentedBuffer = ArrayPool<byte>.Shared.Rent(toReturn.Length * 2);

            Buffer.BlockCopy(
                toReturn,
                _topOfStack,
                _rentedBuffer,
                _rentedBuffer.Length - toReturn.Length + _topOfStack,
                toReturn.Length - _topOfStack);

            _topOfStack += _rentedBuffer.Length - toReturn.Length;

            // The data in this rented buffer only conveys the positions and
            // lengths of tokens in a document, but no content; so it does not
            // need to be cleared.
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }
}