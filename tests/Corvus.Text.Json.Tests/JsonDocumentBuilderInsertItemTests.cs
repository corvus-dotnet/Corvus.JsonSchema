// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
[TestClass]
public class JsonDocumentBuilderInsertItemTests
{
    #region InsertItem and InsertItemNull Tests

    [TestMethod]
    public void InsertItem_AtBeginningOfArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(0, 0);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(0, root[0].GetInt32());
        Assert.AreEqual(1, root[1].GetInt32());
        Assert.AreEqual(2, root[2].GetInt32());
        Assert.AreEqual(3, root[3].GetInt32());
    }

    [TestMethod]
    public void InsertItem_InMiddleOfArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(99, root[1].GetInt32());
        Assert.AreEqual(2, root[2].GetInt32());
        Assert.AreEqual(3, root[3].GetInt32());
    }

    [TestMethod]
    public void InsertItem_AtEndOfArray_AppendsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(3, 4);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
        Assert.AreEqual(4, root[3].GetInt32());
    }

    [TestMethod]
    public void InsertItem_InEmptyArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(0, 42);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(42, root[0].GetInt32());
    }

    [TestMethod]
    public void InsertItem_String_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, "inserted");

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual("inserted", root[1].GetString());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Bool_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, true);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.IsTrue(root[1].GetBoolean());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItemNull_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(1);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root[1].ValueKind);
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItemNull_AtBeginning_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(0);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(JsonValueKind.Null, root[0].ValueKind);
        Assert.AreEqual(1, root[1].GetInt32());
        Assert.AreEqual(2, root[2].GetInt32());
        Assert.AreEqual(3, root[3].GetInt32());
    }

    [TestMethod]
    public void InsertItemNull_AtEnd_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(3);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root[3].ValueKind);
    }

    [TestMethod]
    public void InsertItem_MultipleInserts_MaintainsOrder()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Insert multiple items
        builderDoc.RootElement.InsertItem(1, 2);
        builderDoc.RootElement.InsertItem(2, 3);
        builderDoc.RootElement.InsertItem(3, 4);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
        Assert.AreEqual(4, root[3].GetInt32());
        Assert.AreEqual(5, root[4].GetInt32());
    }

    [TestMethod]
    public void InsertItem_InNestedArray_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2], [3, 4]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Insert into nested array
        builderDoc.RootElement[0].InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(3, root[0].GetArrayLength());
        Assert.AreEqual(1, root[0][0].GetInt32());
        Assert.AreEqual(99, root[0][1].GetInt32());
        Assert.AreEqual(2, root[0][2].GetInt32());
        Assert.AreEqual(2, root[1].GetArrayLength());
        Assert.AreEqual(3, root[1][0].GetInt32());
        Assert.AreEqual(4, root[1][1].GetInt32());
    }

    [TestMethod]
    public void InsertItem_InNestedObjectArray_PreservesObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"items\": [1, 2, 3]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.GetProperty("items").InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        JsonElement.Mutable items = root.GetProperty("items");
        Assert.AreEqual(4, items.GetArrayLength());
        Assert.AreEqual(1, items[0].GetInt32());
        Assert.AreEqual(99, items[1].GetInt32());
        Assert.AreEqual(2, items[2].GetInt32());
        Assert.AreEqual(3, items[3].GetInt32());
    }

    [TestMethod]
    public void InsertItem_WithMixedTypes_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"text\", true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(1);
        builderDoc.RootElement.InsertItem(2, 3.14);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root[1].ValueKind);
        Assert.AreEqual(3.14, root[2].GetDouble());
        Assert.AreEqual("text", root[3].GetString());
        Assert.IsTrue(root[4].GetBoolean());
    }

    [TestMethod]
    public void InsertItem_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItem(-1, 99));
    }

    [TestMethod]
    public void InsertItem_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItem(3, 99));
    }

    [TestMethod]
    public void InsertItemNull_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItemNull(-1));
    }

    [TestMethod]
    public void InsertItemNull_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItemNull(3));
    }

    [TestMethod]
    public void InsertItem_ComplexNestedStructure_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2], {\"nested\": [3, 4]}, [5]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Insert into deeply nested array
        builderDoc.RootElement[1].GetProperty("nested").InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable nestedArray = root[1].GetProperty("nested");
        Assert.AreEqual(3, nestedArray.GetArrayLength());
        Assert.AreEqual(3, nestedArray[0].GetInt32());
        Assert.AreEqual(99, nestedArray[1].GetInt32());
        Assert.AreEqual(4, nestedArray[2].GetInt32());
    }

    #endregion

  #region Additional Type Overload Tests

    [TestMethod]
    public void InsertItem_Guid_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        var testGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        builderDoc.RootElement.InsertItem(1, testGuid);

 // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
     Assert.AreEqual(3, root.GetArrayLength());
Assert.AreEqual(1, root[0].GetInt32());
      Assert.AreEqual(testGuid, root[1].GetGuid());
Assert.AreEqual(2, root[2].GetInt32());
    }

[TestMethod]
   public void InsertItem_DateTime_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
   using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

  // Act
builderDoc.RootElement.InsertItem(1, testDate);

        // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
    Assert.AreEqual(1, root[0].GetInt32());
    Assert.AreEqual(testDate, root[1].GetDateTime());
        Assert.AreEqual(2, root[2].GetInt32());
  }

    [TestMethod]
    public void InsertItem_DateTimeOffset_Works()
{
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
var testDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2));

   // Act
 builderDoc.RootElement.InsertItem(1, testDate);

 // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
Assert.AreEqual(1, root[0].GetInt32());
   Assert.AreEqual(testDate, root[1].GetDateTimeOffset());
  Assert.AreEqual(2, root[2].GetInt32());
  }

  [TestMethod]
public void InsertItem_Byte_Works()
    {
// Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, (byte)255);

 // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
 Assert.AreEqual(3, root.GetArrayLength());
    Assert.AreEqual(1, root[0].GetInt32());
 Assert.AreEqual(255, root[1].GetByte());
        Assert.AreEqual(2, root[2].GetInt32());
}

    [TestMethod]
    public void InsertItem_SByte_Works()
    {
      // Arrange
    using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
    using var workspace = JsonWorkspace.Create();
using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, (sbyte)-100);

        // Assert
    JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(-100, root[1].GetSByte());
  Assert.AreEqual(2, root[2].GetInt32());
    }

   [TestMethod]
public void InsertItem_Short_Works()
    {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
    using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

 // Act
      builderDoc.RootElement.InsertItem(1, (short)1000);

        // Assert
    JsonElement.Mutable root = builderDoc.RootElement;
 Assert.AreEqual(3, root.GetArrayLength());
  Assert.AreEqual(1, root[0].GetInt32());
     Assert.AreEqual(1000, root[1].GetInt16());
     Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
  public void InsertItem_UShort_Works()
    {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
 builderDoc.RootElement.InsertItem(1, (ushort)50000);

// Assert
 JsonElement.Mutable root = builderDoc.RootElement;
  Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(50000, root[1].GetUInt16());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Int_Works()
    {
  // Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
 using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
        builderDoc.RootElement.InsertItem(1, 123456);

  // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
 Assert.AreEqual(3, root.GetArrayLength());
Assert.AreEqual(1, root[0].GetInt32());
     Assert.AreEqual(123456, root[1].GetInt32());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_UInt_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
      using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, 3000000000u);

   // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(3000000000u, root[1].GetUInt32());
  Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Long_Works()
    {
 // Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
     builderDoc.RootElement.InsertItem(1, 9876543210L);

    // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
   Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
   Assert.AreEqual(9876543210L, root[1].GetInt64());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_ULong_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
        builderDoc.RootElement.InsertItem(1, 18446744073709551600UL);

 // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
 Assert.AreEqual(1, root[0].GetInt32());
Assert.AreEqual(18446744073709551600UL, root[1].GetUInt64());
 Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Float_Works()
  {
// Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, 3.14f);

   // Assert
   JsonElement.Mutable root = builderDoc.RootElement;
  Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(3.14f, root[1].GetSingle());
 Assert.AreEqual(2, root[2].GetInt32());
    }

  [TestMethod]
  public void InsertItem_Double_Works()
 {
    // Arrange
using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
     using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, 2.718281828);

      // Assert
   JsonElement.Mutable root = builderDoc.RootElement;
  Assert.AreEqual(3, root.GetArrayLength());
 Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2.718281828, root[1].GetDouble());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Decimal_Works()
{
        // Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

 // Act
        builderDoc.RootElement.InsertItem(1, 123.456m);

        // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
   Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
    Assert.AreEqual(123.456m, root[1].GetDecimal());
        Assert.AreEqual(2, root[2].GetInt32());
   }

#if NET
    [TestMethod]
    public void InsertItem_Int128_Works()
    {
   // Arrange
   using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
   using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   var bigNumber = Int128.Parse("170141183460469231731687303715884105727");

        // Act
   builderDoc.RootElement.InsertItem(1, bigNumber);

        // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
      Assert.AreEqual(3, root.GetArrayLength());
 Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(bigNumber, root[1].GetInt128());
        Assert.AreEqual(2, root[2].GetInt32());
}

 [TestMethod]
    public void InsertItem_UInt128_Works()
    {
        // Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
   using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   var bigNumber = UInt128.Parse("340282366920938463463374607431768211455");

        // Act
   builderDoc.RootElement.InsertItem(1, bigNumber);

      // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
    Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
   Assert.AreEqual(bigNumber, root[1].GetUInt128());
    Assert.AreEqual(2, root[2].GetInt32());
  }

    [TestMethod]
public void InsertItem_Half_Works()
   {
        // Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        var halfValue = (Half)1.5;

   // Act
   builderDoc.RootElement.InsertItem(1, halfValue);

      // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
 Assert.AreEqual(3, root.GetArrayLength());
      Assert.AreEqual(1, root[0].GetInt32());
    Assert.AreEqual(halfValue, root[1].GetHalf());
        Assert.AreEqual(2, root[2].GetInt32());
}
#endif

    [TestMethod]
    public void InsertItem_Object_Works()
   {
   // Arrange
    using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
using var workspace = JsonWorkspace.Create();
 using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, (ref o) =>
{
 o.AddProperty("name", "test");
      o.AddProperty("value", 42);
        });

     // Assert
 JsonElement.Mutable root = builderDoc.RootElement;
   Assert.AreEqual(3, root.GetArrayLength());
   Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(JsonValueKind.Object, root[1].ValueKind);
    Assert.AreEqual("test", root[1].GetProperty("name").GetString());
    Assert.AreEqual(42, root[1].GetProperty("value").GetInt32());
 Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
public void InsertItem_Array_Works()
    {
     // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
    using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

 // Act
        builderDoc.RootElement.InsertItem(1, (ref a) =>
 {
       a.AddItem(10);
  a.AddItem(20);
a.AddItem(30);
});

 // Assert
    JsonElement.Mutable root = builderDoc.RootElement;
Assert.AreEqual(3, root.GetArrayLength());
   Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(JsonValueKind.Array, root[1].ValueKind);
        Assert.AreEqual(3, root[1].GetArrayLength());
Assert.AreEqual(10, root[1][0].GetInt32());
Assert.AreEqual(20, root[1][1].GetInt32());
   Assert.AreEqual(30, root[1][2].GetInt32());
   Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_OffsetDateTime_Works()
 {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
      using var workspace = JsonWorkspace.Create();
    using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   var testDate = new OffsetDateTime(
            new LocalDateTime(2024, 1, 15, 10, 30, 0),
 Offset.FromHours(2));

// Act
        builderDoc.RootElement.InsertItem(1, testDate);

     // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(testDate, root[1].GetOffsetDateTime());
Assert.AreEqual(2, root[2].GetInt32());
    }

   [TestMethod]
 public void InsertItem_OffsetDate_Works()
    {
  // Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
   using var workspace = JsonWorkspace.Create();
      using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        var testDate = new OffsetDate(
new LocalDate(2024, 1, 15),
        Offset.FromHours(2));

        // Act
        builderDoc.RootElement.InsertItem(1, testDate);

// Assert
  JsonElement.Mutable root = builderDoc.RootElement;
   Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
Assert.AreEqual(testDate, root[1].GetOffsetDate());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_OffsetTime_Works()
  {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   var testTime = new OffsetTime(
  new LocalTime(10, 30, 0),
   Offset.FromHours(2));

        // Act
   builderDoc.RootElement.InsertItem(1, testTime);

        // Assert
JsonElement.Mutable root = builderDoc.RootElement;
    Assert.AreEqual(3, root.GetArrayLength());
 Assert.AreEqual(1, root[0].GetInt32());
Assert.AreEqual(testTime, root[1].GetOffsetTime());
 Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_LocalDate_Works()
    {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   var testDate = new LocalDate(2024, 1, 15);

 // Act
        builderDoc.RootElement.InsertItem(1, testDate);

// Assert
    JsonElement.Mutable root = builderDoc.RootElement;
 Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
   Assert.AreEqual(testDate, root[1].GetLocalDate());
      Assert.AreEqual(2, root[2].GetInt32());
    }

    [TestMethod]
    public void InsertItem_Period_Works()
    {
    // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
   Period testPeriod = Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3);

// Act
      builderDoc.RootElement.InsertItem(1, testPeriod);

        // Assert
   JsonElement.Mutable root = builderDoc.RootElement;
Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(testPeriod, root[1].GetPeriod());
    Assert.AreEqual(2, root[2].GetInt32());
  }

    [TestMethod]
 public void InsertItem_Utf8String_Works()
    {
   // Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
   using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        byte[] utf8String = "test string"u8.ToArray();

// Act
builderDoc.RootElement.InsertItem(1, utf8String.AsSpan());

  // Assert
 JsonElement.Mutable root = builderDoc.RootElement;
  Assert.AreEqual(3, root.GetArrayLength());
  Assert.AreEqual(1, root[0].GetInt32());
      Assert.AreEqual("test string", root[1].GetString());
 Assert.AreEqual(2, root[2].GetInt32());
  }

    [TestMethod]
  public void InsertItem_CharSpanString_Works()
    {
  // Arrange
using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
      using var workspace = JsonWorkspace.Create();
   using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
    builderDoc.RootElement.InsertItem(1, "char span".AsSpan());

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
   Assert.AreEqual(1, root[0].GetInt32());
Assert.AreEqual("char span", root[1].GetString());
        Assert.AreEqual(2, root[2].GetInt32());
    }

    #endregion

    #region AddItem and AddItemNull Tests

    [TestMethod]
    public void AddItem_Int_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(4);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
        Assert.AreEqual(4, root[3].GetInt32());
    }

    [TestMethod]
    public void AddItem_String_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""["a","b"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem("c");

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("a", root[0].GetString());
        Assert.AreEqual("b", root[1].GetString());
        Assert.AreEqual("c", root[2].GetString());
    }

    [TestMethod]
    public void AddItem_Bool_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(false);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.IsTrue(root[0].GetBoolean());
        Assert.IsFalse(root[1].GetBoolean());
    }

    [TestMethod]
    public void AddItemNull_AppendsNullToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItemNull();

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root[2].ValueKind);
    }

    [TestMethod]
    public void AddItem_ToEmptyArray_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(42);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(42, root[0].GetInt32());
    }

    [TestMethod]
    public void AddItem_MultipleAdds_PreservesOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(1);
        builderDoc.RootElement.AddItem(2);
        builderDoc.RootElement.AddItem(3);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
    }

    [TestMethod]
    public void AddItem_Object_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(static (ref b) =>
        {
            b.AddProperty("name", "test");
            b.AddProperty("value", 42);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual("test", root[0].GetProperty("name").GetString());
        Assert.AreEqual(42, root[0].GetProperty("value").GetInt32());
    }

    [TestMethod]
    public void AddItem_Array_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(static (ref b) =>
        {
            b.AddItem(2);
            b.AddItem(3);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(1, root[0][0].GetInt32());
        Assert.AreEqual(2, root[1][0].GetInt32());
        Assert.AreEqual(3, root[1][1].GetInt32());
    }

    [TestMethod]
    public void AddItem_WithUndefinedSource_IsNoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(default(JsonElement.Source));

        Assert.AreEqual(3, builderDoc.RootElement.GetArrayLength());
    }

    #endregion
}
