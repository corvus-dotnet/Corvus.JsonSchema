// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;
public static class JsonDocumentBuilderInsertItemTests
{
    #region InsertItem and InsertItemNull Tests

    [Fact]
    public static void InsertItem_AtBeginningOfArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(0, 0);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(0, root[0].GetInt32());
        Assert.Equal(1, root[1].GetInt32());
        Assert.Equal(2, root[2].GetInt32());
        Assert.Equal(3, root[3].GetInt32());
    }

    [Fact]
    public static void InsertItem_InMiddleOfArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(99, root[1].GetInt32());
        Assert.Equal(2, root[2].GetInt32());
        Assert.Equal(3, root[3].GetInt32());
    }

    [Fact]
    public static void InsertItem_AtEndOfArray_AppendsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(3, 4);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public static void InsertItem_InEmptyArray_InsertsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(0, 42);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal(42, root[0].GetInt32());
    }

    [Fact]
    public static void InsertItem_String_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, "inserted");

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal("inserted", root[1].GetString());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Bool_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, true);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.True(root[1].GetBoolean());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItemNull_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(1);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(JsonValueKind.Null, root[1].ValueKind);
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItemNull_AtBeginning_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(0);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, root[0].ValueKind);
        Assert.Equal(1, root[1].GetInt32());
        Assert.Equal(2, root[2].GetInt32());
        Assert.Equal(3, root[3].GetInt32());
    }

    [Fact]
    public static void InsertItemNull_AtEnd_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItemNull(3);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(JsonValueKind.Null, root[3].ValueKind);
    }

    [Fact]
    public static void InsertItem_MultipleInserts_MaintainsOrder()
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
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
        Assert.Equal(5, root[4].GetInt32());
    }

    [Fact]
    public static void InsertItem_InNestedArray_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2], [3, 4]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Insert into nested array
        builderDoc.RootElement[0].InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(3, root[0].GetArrayLength());
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(99, root[0][1].GetInt32());
        Assert.Equal(2, root[0][2].GetInt32());
        Assert.Equal(2, root[1].GetArrayLength());
        Assert.Equal(3, root[1][0].GetInt32());
        Assert.Equal(4, root[1][1].GetInt32());
    }

    [Fact]
    public static void InsertItem_InNestedObjectArray_PreservesObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"items\": [1, 2, 3]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.GetProperty("items").InsertItem(1, 99);

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        JsonElement.Mutable items = root.GetProperty("items");
        Assert.Equal(4, items.GetArrayLength());
        Assert.Equal(1, items[0].GetInt32());
        Assert.Equal(99, items[1].GetInt32());
        Assert.Equal(2, items[2].GetInt32());
        Assert.Equal(3, items[3].GetInt32());
    }

    [Fact]
    public static void InsertItem_WithMixedTypes_Works()
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
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(JsonValueKind.Null, root[1].ValueKind);
        Assert.Equal(3.14, root[2].GetDouble());
        Assert.Equal("text", root[3].GetString());
        Assert.True(root[4].GetBoolean());
    }

    [Fact]
    public static void InsertItem_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItem(-1, 99));
    }

    [Fact]
    public static void InsertItem_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItem(3, 99));
    }

    [Fact]
    public static void InsertItemNull_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItemNull(-1));
    }

    [Fact]
    public static void InsertItemNull_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => builderDoc.RootElement.InsertItemNull(3));
    }

    [Fact]
    public static void InsertItem_ComplexNestedStructure_Works()
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
        Assert.Equal(3, nestedArray.GetArrayLength());
        Assert.Equal(3, nestedArray[0].GetInt32());
        Assert.Equal(99, nestedArray[1].GetInt32());
        Assert.Equal(4, nestedArray[2].GetInt32());
    }

    #endregion

  #region Additional Type Overload Tests

    [Fact]
    public static void InsertItem_Guid_Works()
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
     Assert.Equal(3, root.GetArrayLength());
Assert.Equal(1, root[0].GetInt32());
      Assert.Equal(testGuid, root[1].GetGuid());
Assert.Equal(2, root[2].GetInt32());
    }

[Fact]
   public static void InsertItem_DateTime_Works()
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
        Assert.Equal(3, root.GetArrayLength());
    Assert.Equal(1, root[0].GetInt32());
    Assert.Equal(testDate, root[1].GetDateTime());
        Assert.Equal(2, root[2].GetInt32());
  }

    [Fact]
    public static void InsertItem_DateTimeOffset_Works()
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
        Assert.Equal(3, root.GetArrayLength());
Assert.Equal(1, root[0].GetInt32());
   Assert.Equal(testDate, root[1].GetDateTimeOffset());
  Assert.Equal(2, root[2].GetInt32());
  }

  [Fact]
public static void InsertItem_Byte_Works()
    {
// Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, (byte)255);

 // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
 Assert.Equal(3, root.GetArrayLength());
    Assert.Equal(1, root[0].GetInt32());
 Assert.Equal(255, root[1].GetByte());
        Assert.Equal(2, root[2].GetInt32());
}

    [Fact]
    public static void InsertItem_SByte_Works()
    {
      // Arrange
    using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
    using var workspace = JsonWorkspace.Create();
using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, (sbyte)-100);

        // Assert
    JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(-100, root[1].GetSByte());
  Assert.Equal(2, root[2].GetInt32());
    }

   [Fact]
public static void InsertItem_Short_Works()
    {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
    using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

 // Act
      builderDoc.RootElement.InsertItem(1, (short)1000);

        // Assert
    JsonElement.Mutable root = builderDoc.RootElement;
 Assert.Equal(3, root.GetArrayLength());
  Assert.Equal(1, root[0].GetInt32());
     Assert.Equal(1000, root[1].GetInt16());
     Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
  public static void InsertItem_UShort_Works()
    {
   // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
 builderDoc.RootElement.InsertItem(1, (ushort)50000);

// Assert
 JsonElement.Mutable root = builderDoc.RootElement;
  Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(50000, root[1].GetUInt16());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Int_Works()
    {
  // Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
 using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
        builderDoc.RootElement.InsertItem(1, 123456);

  // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
 Assert.Equal(3, root.GetArrayLength());
Assert.Equal(1, root[0].GetInt32());
     Assert.Equal(123456, root[1].GetInt32());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_UInt_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
      using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, 3000000000u);

   // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(3000000000u, root[1].GetUInt32());
  Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Long_Works()
    {
 // Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
     builderDoc.RootElement.InsertItem(1, 9876543210L);

    // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
   Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
   Assert.Equal(9876543210L, root[1].GetInt64());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_ULong_Works()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
 using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

// Act
        builderDoc.RootElement.InsertItem(1, 18446744073709551600UL);

 // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
 Assert.Equal(1, root[0].GetInt32());
Assert.Equal(18446744073709551600UL, root[1].GetUInt64());
 Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Float_Works()
  {
// Arrange
 using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
  using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.InsertItem(1, 3.14f);

   // Assert
   JsonElement.Mutable root = builderDoc.RootElement;
  Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(3.14f, root[1].GetSingle());
 Assert.Equal(2, root[2].GetInt32());
    }

  [Fact]
  public static void InsertItem_Double_Works()
 {
    // Arrange
using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
     using var workspace = JsonWorkspace.Create();
  using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

  // Act
        builderDoc.RootElement.InsertItem(1, 2.718281828);

      // Assert
   JsonElement.Mutable root = builderDoc.RootElement;
  Assert.Equal(3, root.GetArrayLength());
 Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2.718281828, root[1].GetDouble());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Decimal_Works()
{
        // Arrange
  using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

 // Act
        builderDoc.RootElement.InsertItem(1, 123.456m);

        // Assert
  JsonElement.Mutable root = builderDoc.RootElement;
   Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
    Assert.Equal(123.456m, root[1].GetDecimal());
        Assert.Equal(2, root[2].GetInt32());
   }

#if NET
    [Fact]
    public static void InsertItem_Int128_Works()
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
      Assert.Equal(3, root.GetArrayLength());
 Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(bigNumber, root[1].GetInt128());
        Assert.Equal(2, root[2].GetInt32());
}

 [Fact]
    public static void InsertItem_UInt128_Works()
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
    Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
   Assert.Equal(bigNumber, root[1].GetUInt128());
    Assert.Equal(2, root[2].GetInt32());
  }

    [Fact]
public static void InsertItem_Half_Works()
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
 Assert.Equal(3, root.GetArrayLength());
      Assert.Equal(1, root[0].GetInt32());
    Assert.Equal(halfValue, root[1].GetHalf());
        Assert.Equal(2, root[2].GetInt32());
}
#endif

    [Fact]
    public static void InsertItem_Object_Works()
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
   Assert.Equal(3, root.GetArrayLength());
   Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(JsonValueKind.Object, root[1].ValueKind);
    Assert.Equal("test", root[1].GetProperty("name").GetString());
    Assert.Equal(42, root[1].GetProperty("value").GetInt32());
 Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
public static void InsertItem_Array_Works()
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
Assert.Equal(3, root.GetArrayLength());
   Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(JsonValueKind.Array, root[1].ValueKind);
        Assert.Equal(3, root[1].GetArrayLength());
Assert.Equal(10, root[1][0].GetInt32());
Assert.Equal(20, root[1][1].GetInt32());
   Assert.Equal(30, root[1][2].GetInt32());
   Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_OffsetDateTime_Works()
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
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(testDate, root[1].GetOffsetDateTime());
Assert.Equal(2, root[2].GetInt32());
    }

   [Fact]
 public static void InsertItem_OffsetDate_Works()
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
   Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
Assert.Equal(testDate, root[1].GetOffsetDate());
        Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_OffsetTime_Works()
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
    Assert.Equal(3, root.GetArrayLength());
 Assert.Equal(1, root[0].GetInt32());
Assert.Equal(testTime, root[1].GetOffsetTime());
 Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_LocalDate_Works()
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
 Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
   Assert.Equal(testDate, root[1].GetLocalDate());
      Assert.Equal(2, root[2].GetInt32());
    }

    [Fact]
    public static void InsertItem_Period_Works()
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
Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(testPeriod, root[1].GetPeriod());
    Assert.Equal(2, root[2].GetInt32());
  }

    [Fact]
 public static void InsertItem_Utf8String_Works()
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
  Assert.Equal(3, root.GetArrayLength());
  Assert.Equal(1, root[0].GetInt32());
      Assert.Equal("test string", root[1].GetString());
 Assert.Equal(2, root[2].GetInt32());
  }

    [Fact]
  public static void InsertItem_CharSpanString_Works()
    {
  // Arrange
using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
      using var workspace = JsonWorkspace.Create();
   using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
    builderDoc.RootElement.InsertItem(1, "char span".AsSpan());

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
   Assert.Equal(1, root[0].GetInt32());
Assert.Equal("char span", root[1].GetString());
        Assert.Equal(2, root[2].GetInt32());
    }

    #endregion

    #region AddItem and AddItemNull Tests

    [Fact]
    public static void AddItem_Int_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(4);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public static void AddItem_String_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""["a","b"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem("c");

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("a", root[0].GetString());
        Assert.Equal("b", root[1].GetString());
        Assert.Equal("c", root[2].GetString());
    }

    [Fact]
    public static void AddItem_Bool_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(false);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.True(root[0].GetBoolean());
        Assert.False(root[1].GetBoolean());
    }

    [Fact]
    public static void AddItemNull_AppendsNullToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItemNull();

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(JsonValueKind.Null, root[2].ValueKind);
    }

    [Fact]
    public static void AddItem_ToEmptyArray_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(42);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal(42, root[0].GetInt32());
    }

    [Fact]
    public static void AddItem_MultipleAdds_PreservesOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(1);
        builderDoc.RootElement.AddItem(2);
        builderDoc.RootElement.AddItem(3);

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    [Fact]
    public static void AddItem_Object_AppendsToEnd()
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
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal("test", root[0].GetProperty("name").GetString());
        Assert.Equal(42, root[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public static void AddItem_Array_AppendsToEnd()
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
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(2, root[1][0].GetInt32());
        Assert.Equal(3, root[1][1].GetInt32());
    }

    [Fact]
    public static void AddItem_WithUndefinedSource_IsNoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddItem(default(JsonElement.Source));

        Assert.Equal(3, builderDoc.RootElement.GetArrayLength());
    }

    #endregion
}
