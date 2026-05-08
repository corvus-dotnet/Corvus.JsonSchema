// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class BitStackTests
{
    private static readonly Random s_random = new(42);

    [TestMethod]
    [DataRow(32)]
    [DataRow(64)]
    [DataRow(256)]
    public void BitStackPushPop(int bitLength)
    {
        BitStack bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);

        bool[] values = new bool[bitLength];
        for (int i = 0; i < bitLength; i++)
        {
            values[i] = s_random.NextDouble() >= 0.5;
        }

        for (int i = 0; i < bitLength; i++)
        {
            if (values[i])
            {
                bitStack.PushTrue();
            }
            else
            {
                bitStack.PushFalse();
            }
            Assert.AreEqual(i + 1, bitStack.CurrentDepth);
        }

        // Loop backwards when popping.
        for (int i = bitLength - 1; i > 0; i--)
        {
            // We need the value at the top *after* popping off the last one.
            Assert.AreEqual(values[i - 1], bitStack.Pop());
            Assert.AreEqual(i, bitStack.CurrentDepth);
        }
    }

    [TestMethod]
    [DataRow(3_200_000)]
    [DataRow(int.MaxValue / 32 + 1)]    // 67_108_864
    public void BitStackPushPopLarge(int bitLength)
    {
        BitStack bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);

        bool[] values = new bool[bitLength];
        for (int i = 0; i < bitLength; i++)
        {
            values[i] = s_random.NextDouble() >= 0.5;
        }

        const int IterationCapacity = 1_600_000;

        int expectedDepth = 0;
        // Only set and compare the first and last few (otherwise, the test takes too long)
        for (int i = 0; i < IterationCapacity; i++)
        {
            if (values[i])
            {
                bitStack.PushTrue();
            }
            else
            {
                bitStack.PushFalse();
            }
            expectedDepth++;
            Assert.AreEqual(expectedDepth, bitStack.CurrentDepth);
        }
        for (int i = bitLength - IterationCapacity; i < bitLength; i++)
        {
            if (values[i])
            {
                bitStack.PushTrue();
            }
            else
            {
                bitStack.PushFalse();
            }
            expectedDepth++;
            Assert.AreEqual(expectedDepth, bitStack.CurrentDepth);
        }

        Assert.AreEqual(IterationCapacity * 2, expectedDepth);

        // Loop backwards when popping.
        for (int i = bitLength - 1; i >= bitLength - IterationCapacity; i--)
        {
            // We need the value at the top *after* popping off the last one.
            Assert.AreEqual(values[i - 1], bitStack.Pop());

            expectedDepth--;
            Assert.AreEqual(expectedDepth, bitStack.CurrentDepth);
        }
        for (int i = IterationCapacity - 1; i > 0; i--)
        {
            // We need the value at the top *after* popping off the last one.
            Assert.AreEqual(values[i - 1], bitStack.Pop());

            expectedDepth--;
            Assert.AreEqual(expectedDepth, bitStack.CurrentDepth);
        }
    }

    [TestMethod]
    public void DefaultBitStack()
    {
        BitStack bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);
    }

    [TestMethod]
    public void SetResetFirstBit()
    {
        BitStack bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);
        bitStack.SetFirstBit();
        Assert.AreEqual(1, bitStack.CurrentDepth);
        Assert.IsFalse(bitStack.Pop());
        Assert.AreEqual(0, bitStack.CurrentDepth);

        bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);
        bitStack.ResetFirstBit();
        Assert.AreEqual(1, bitStack.CurrentDepth);
        Assert.IsFalse(bitStack.Pop());
        Assert.AreEqual(0, bitStack.CurrentDepth);

        bitStack = default;
        Assert.AreEqual(0, bitStack.CurrentDepth);
        bitStack.SetFirstBit();
        Assert.AreEqual(1, bitStack.CurrentDepth);
        Assert.IsFalse(bitStack.Pop());
        Assert.AreEqual(0, bitStack.CurrentDepth);
        bitStack.ResetFirstBit();
        Assert.AreEqual(1, bitStack.CurrentDepth);
        Assert.IsFalse(bitStack.Pop());
        Assert.AreEqual(0, bitStack.CurrentDepth);
    }
}
