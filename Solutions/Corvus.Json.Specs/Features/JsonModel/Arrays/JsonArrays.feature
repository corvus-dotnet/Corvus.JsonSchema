﻿Feature: JsonArrays
	Getting, setting, adding and removing elements in an array.

Scenario Outline: Remove items from a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I remove the item at index <itemIndex> from the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | result         |
	| JsonArray     | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonAny       | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonArray     | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonAny       | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonArray     | 2         | ["foo", "bar", 3] | ["foo", "bar"] |
	| JsonAny       | 2         | ["foo", "bar", 3] | ["foo", "bar"] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | ["foo", "bar"] |

Scenario Outline: Add items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the item <newValue> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue | value             | result                   |
	| JsonArray     | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |
	| JsonAny       | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |
	| JsonNotAny    | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |

Scenario Outline: Add two items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the 2 items <newValue1> and <newValue2> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | value             | result                          |
	| JsonArray     | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |
	| JsonAny       | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |
	| JsonNotAny    | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |

Scenario Outline: Add three items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the 3 items <newValue1>, <newValue2> and <newValue3> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | value             | result                                 |
	| JsonArray     | "baz"     | "fip"     | "zip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip"] |
	| JsonNotAny    | "baz"     | "fip"     | "zip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip"] |

Scenario Outline: Add four items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the 4 items <newValue1>, <newValue2>, <newValue3> and <newValue4> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | newValue4 | value             | result                                         |
	| JsonArray     | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |
	| JsonNotAny    | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |

Scenario Outline: Add many items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the 5 items <newValue1>, <newValue2>, <newValue3>, <newValue4> and <newValue5> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | newValue4 | newValue5 | value             | result                                                   |
	| JsonArray     | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonNotAny    | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |

Scenario Outline: Add a range of items to a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I add the range <newValues> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValues                               | value             | result                                                   |
	| JsonArray     | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonAny       | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonNotAny    | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |


Scenario Outline: Set items to JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I set the item in the <jsonValueType> at index <itemIndex> to the value <itemValue>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | itemValue | result                |
	| JsonArray     | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonAny       | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonArray     | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonAny       | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonArray     | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |
	| JsonAny       | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |

Scenario Outline: Remove items from a JsonElement backed JsonArray where the index is out of range
	Given the JsonElement backed <jsonValueType>  <value>
	When I remove the item at index <itemIndex> from the <jsonValueType>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             |
	| JsonArray     | 3         | ["foo", "bar", 3] |
	| JsonAny       | 3         | ["foo", "bar", 3] |
	| JsonNotAny    | 3         | ["foo", "bar", 3] |

Scenario Outline: Set items to JsonElement backed JsonArray where the index is out of range
	Given the JsonElement backed <jsonValueType> <value>
	When I set the item in the <jsonValueType> at index <itemIndex> to the value <itemValue>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             | itemValue |
	| JsonArray     | 3         | ["foo", "bar", 3] | "baz"     |
	| JsonAny       | 3         | ["foo", "bar", 3] | "baz"     |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | "baz"     |

Scenario Outline: Get items from a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I get the <itemType> in the <jsonValueType> at index <itemIndex>
	Then the <itemType> should equal <expectedValue>

Examples:
	| jsonValueType | itemIndex | value             | itemType   | expectedValue |
	| JsonArray     | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonAny       | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonArray     | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonAny       | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonArray     | 2         | ["foo", "bar", 3] | JsonNumber | 3             |
	| JsonAny       | 2         | ["foo", "bar", 3] | JsonNumber | 3             |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | JsonNumber | 3             |

Scenario Outline: Get items from a JsonElement backed JsonArray where the index is out of range
	Given the JsonElement backed <jsonValueType> <value>
	When I get the <itemType> in the <jsonValueType> at index <itemIndex>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             | itemType   |
	| JsonArray     | 3         | ["foo", "bar", 3] | JsonString |
	| JsonAny       | 3         | ["foo", "bar", 3] | JsonString |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | JsonString |

Scenario Outline: Insert items into JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I insert the item <itemValue> in the <jsonValueType> at index <itemIndex>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | itemValue | result                   |
	| JsonArray     | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonAny       | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonArray     | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonAny       | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonArray     | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonAny       | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonArray     | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |
	| JsonAny       | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |

Scenario Outline: Replace items in a JsonElement backed JsonArray
	Given the JsonElement backed <jsonValueType> <value>
	When I replace the item <oldValue> in the <jsonValueType> with the value <newValue>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | value             | oldValue | newValue | result                |
	| JsonArray     | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonAny       | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonNotAny    | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonArray     | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonAny       | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonNotAny    | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonArray     | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |
	| JsonAny       | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |
	| JsonNotAny    | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |

Scenario Outline: Remove items from a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I remove the item at index <itemIndex> from the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | result         |
	| JsonArray     | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonAny       | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | ["bar", 3]     |
	| JsonArray     | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonAny       | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | ["foo", 3]     |
	| JsonArray     | 2         | ["foo", "bar", 3] | ["foo", "bar"] |
	| JsonAny       | 2         | ["foo", "bar", 3] | ["foo", "bar"] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | ["foo", "bar"] |

Scenario Outline: Add items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the item <newValue> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue | value             | result                   |
	| JsonArray     | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |
	| JsonAny       | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |
	| JsonNotAny    | "baz"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz"] |

Scenario Outline: Add two items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the 2 items <newValue1> and <newValue2> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | value             | result                          |
	| JsonArray     | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |
	| JsonAny       | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |
	| JsonNotAny    | "baz"     | "fip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip"] |

Scenario Outline: Add three items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the 3 items <newValue1>, <newValue2> and <newValue3> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | value             | result                                 |
	| JsonArray     | "baz"     | "fip"     | "zip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip"] |

Scenario Outline: Add four items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the 4 items <newValue1>, <newValue2>, <newValue3> and <newValue4> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | newValue4 | value             | result                                         |
	| JsonArray     | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |
	| JsonNotAny    | "baz"     | "fip"     | "zip"     | "bing"    | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing"] |

Scenario Outline: Add many items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the 5 items <newValue1>, <newValue2>, <newValue3>, <newValue4> and <newValue5> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValue1 | newValue2 | newValue3 | newValue4 | newValue5 | value             | result                                                   |
	| JsonArray     | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonAny       | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonNotAny    | "baz"     | "fip"     | "zip"     | "bing"    | "wobble"  | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |

Scenario Outline: Add a range of items to a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I add the range <newValues> to the <jsonValueType>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | newValues                               | value             | result                                                   |
	| JsonArray     | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonAny       | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |
	| JsonNotAny    | ["baz", "fip", "zip", "bing", "wobble"] | ["foo", "bar", 3] | ["foo", "bar", 3, "baz", "fip", "zip", "bing", "wobble"] |


Scenario Outline: Set items to dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I set the item in the <jsonValueType> at index <itemIndex> to the value <itemValue>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | itemValue | result                |
	| JsonArray     | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonAny       | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "bar", 3]     |
	| JsonArray     | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonAny       | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", 3]     |
	| JsonArray     | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |
	| JsonAny       | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz"] |

Scenario Outline: Remove items from a dotnet backed JsonArray where the index is out of range
	Given the dotnet backed <jsonValueType>  <value>
	When I remove the item at index <itemIndex> from the <jsonValueType>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             |
	| JsonArray     | 3         | ["foo", "bar", 3] |
	| JsonAny       | 3         | ["foo", "bar", 3] |
	| JsonNotAny    | 3         | ["foo", "bar", 3] |

Scenario Outline: Set items to dotnet backed JsonArray where the index is out of range
	Given the dotnet backed <jsonValueType> <value>
	When I set the item in the <jsonValueType> at index <itemIndex> to the value <itemValue>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             | itemValue |
	| JsonArray     | 3         | ["foo", "bar", 3] | "baz"     |
	| JsonAny       | 3         | ["foo", "bar", 3] | "baz"     |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | "baz"     |

Scenario Outline: Get items from a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I get the <itemType> in the <jsonValueType> at index <itemIndex>
	Then the <itemType> should equal <expectedValue>

Examples:
	| jsonValueType | itemIndex | value             | itemType   | expectedValue |
	| JsonArray     | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonAny       | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | JsonString | "foo"         |
	| JsonArray     | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonAny       | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | JsonString | "bar"         |
	| JsonArray     | 2         | ["foo", "bar", 3] | JsonNumber | 3             |
	| JsonAny       | 2         | ["foo", "bar", 3] | JsonNumber | 3             |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | JsonNumber | 3             |

Scenario Outline: Get items from a dotnet backed JsonArray where the index is out of range
	Given the dotnet backed <jsonValueType> <value>
	When I get the <itemType> in the <jsonValueType> at index <itemIndex>
	Then the array operation should produce an IndexOutOfRangeException

Examples:
	| jsonValueType | itemIndex | value             | itemType   |
	| JsonArray     | 3         | ["foo", "bar", 3] | JsonString |
	| JsonAny       | 3         | ["foo", "bar", 3] | JsonString |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | JsonString |

Scenario Outline: Insert items into dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I insert the item <itemValue> in the <jsonValueType> at index <itemIndex>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | itemIndex | value             | itemValue | result                   |
	| JsonArray     | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonAny       | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonNotAny    | 0         | ["foo", "bar", 3] | "baz"     | ["baz", "foo", "bar", 3] |
	| JsonArray     | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonAny       | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonNotAny    | 1         | ["foo", "bar", 3] | "baz"     | ["foo", "baz", "bar", 3] |
	| JsonArray     | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonAny       | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonNotAny    | 2         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", "baz", 3] |
	| JsonArray     | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |
	| JsonAny       | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |
	| JsonNotAny    | 3         | ["foo", "bar", 3] | "baz"     | ["foo", "bar", 3, "baz"] |

Scenario Outline: Replace items in a dotnet backed JsonArray
	Given the dotnet backed <jsonValueType> <value>
	When I replace the item <oldValue> in the <jsonValueType> with the value <newValue>
	Then the <jsonValueType> should equal <result>

Examples:
	| jsonValueType | value             | oldValue | newValue | result                |
	| JsonArray     | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonAny       | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonNotAny    | ["foo", "bar", 3] | "foo"    | "baz"    | ["baz", "bar", 3]     |
	| JsonArray     | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonAny       | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonNotAny    | ["foo", "bar", 3] | "bar"    | "baz"    | ["foo", "baz", 3]     |
	| JsonArray     | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |
	| JsonAny       | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |
	| JsonNotAny    | ["foo", "bar", 3] | 3        | "baz"    | ["foo", "bar", "baz"] |