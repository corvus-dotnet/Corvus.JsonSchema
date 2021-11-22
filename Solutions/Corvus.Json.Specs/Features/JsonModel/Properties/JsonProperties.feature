Feature: JsonProperties
	Getting, setting, adding and removing properties.

Scenario Outline: Remove properties from a JsonElement backed JsonProperty
	Given the JsonElement backed <jsonValueType>  <value>
	When I remove the property <propertyName> from the <jsonValueType> using a <propertyNameType>
	Then the property <propertyName> should not be defined using <propertyNameType>

	Examples:
		| jsonValueType | propertyName | value          | propertyNameType   |
		| JsonObject    | foo          | {"foo": "bar"} | string             |
		| JsonAny       | foo          | {"foo": "bar"} | string             |
		| JsonNotAny    | foo          | {"foo": "bar"} | string             |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonObject    | foo          | <undefined>    | string             |
		| JsonAny       | foo          | <undefined>    | string             |
		| JsonNotAny    | foo          | <undefined>    | string             |
		| JsonObject    | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonAny       | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonObject    | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonAny       | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonObject    | foo          | 1.2            | string             |
		| JsonAny       | foo          | 1.2            | string             |
		| JsonNotAny    | foo          | 1.2            | string             |
		| JsonObject    | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonAny       | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonObject    | foo          | 1.2            | ReadOnlySpan<byte> |
		| JsonAny       | foo          | 1.2            | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | 1.2            | ReadOnlySpan<byte> |

Scenario Outline: Set properties to JsonElement backed JsonProperty
	Given the JsonElement backed <jsonValueType> <value>
	When I set the property <propertyName> to the value <propertyValue> on the <jsonValueType> using a <propertyNameType>
	Then the property <propertyName> should be <expectedValue> using <propertyNameType>

	Examples:
		| jsonValueType | propertyName | propertyValue | value          | propertyNameType   | expectedValue |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | 1.2            | string             | <undefined>   |
		| JsonAny       | foo          | "bar"         | 1.2            | string             | <undefined>   |
		| JsonNotAny    | foo          | "bar"         | 1.2            | string             | <undefined>   |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | 1.2            | ReadOnlySpan<char> | <undefined>   |
		| JsonAny       | foo          | "bar"         | 1.2            | ReadOnlySpan<char> | <undefined>   |
		| JsonNotAny    | foo          | "bar"         | 1.2            | ReadOnlySpan<char> | <undefined>   |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |
		| JsonObject    | foo          | "bar"         | 1.2            | ReadOnlySpan<byte> | <undefined>   |
		| JsonAny       | foo          | "bar"         | 1.2            | ReadOnlySpan<byte> | <undefined>   |
		| JsonNotAny    | foo          | "bar"         | 1.2            | ReadOnlySpan<byte> | <undefined>   |

Scenario Outline: Remove properties from a dotnet backed JsonProperty
	Given the dotnet backed <jsonValueType>  <value>
	When I remove the property <propertyName> from the <jsonValueType> using a <propertyNameType>
	Then the property <propertyName> should not be defined using <propertyNameType>

	Examples:
		| jsonValueType | propertyName | value          | propertyNameType   |
		| JsonObject    | foo          | {"foo": "bar"} | string             |
		| JsonAny       | foo          | {"foo": "bar"} | string             |
		| JsonNotAny    | foo          | {"foo": "bar"} | string             |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<char> |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> |
		| JsonObject    | foo          | <undefined>    | string             |
		| JsonAny       | foo          | <undefined>    | string             |
		| JsonNotAny    | foo          | <undefined>    | string             |
		| JsonObject    | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonAny       | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | <undefined>    | ReadOnlySpan<char> |
		| JsonObject    | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonAny       | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | <undefined>    | ReadOnlySpan<byte> |
		| JsonObject    | foo          | 1.2            | string             |
		| JsonAny       | foo          | 1.2            | string             |
		| JsonNotAny    | foo          | 1.2            | string             |
		| JsonObject    | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonAny       | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonNotAny    | foo          | 1.2            | ReadOnlySpan<char> |
		| JsonObject    | foo          | 1.2            | ReadOnlySpan<byte> |
		| JsonAny       | foo          | 1.2            | ReadOnlySpan<byte> |
		| JsonNotAny    | foo          | 1.2            | ReadOnlySpan<byte> |

Scenario Outline: Set properties to dotnet backed JsonProperty
	Given the dotnet backed <jsonValueType> <value>
	When I set the property <propertyName> to the value <propertyValue> on the <jsonValueType> using a <propertyNameType>
	Then the property <propertyName> should be <expectedValue> using <propertyNameType>

	Examples:
		| jsonValueType | propertyName | propertyValue | value          | propertyNameType   | expectedValue |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | string             | "bar"         |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | ReadOnlySpan<char> | "bar"         |
		| JsonObject    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {"foo": "baz"} | ReadOnlySpan<byte> | "bar"         |
		| JsonObject    | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | {}             | ReadOnlySpan<byte> | "bar"         |
		| JsonObject    | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |
		| JsonAny       | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |
		| JsonNotAny    | foo          | "bar"         | <undefined>    | ReadOnlySpan<byte> | "bar"         |

Scenario Outline: Get existing properties for a JsonElement backed JsonProperty
	Given the JsonElement backed <jsonValueType> <value>
	When I try to get the property <propertyName> using <propertyNameType>
	Then the property should <propertyFound>
	And the property value should be <propertyValue>

	Examples:
		| jsonValueType | propertyName | propertyValue | value          | propertyNameType   | propertyFound |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonObject    | foo          | undefined     | {}             | string             | not be found  |
		| JsonAny       | foo          | undefined     | {}             | string             | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | string             | not be found  |
		| JsonObject    | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonAny       | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonObject    | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |

Scenario Outline: Get existing properties for a dotnet backed JsonProperty
	Given the dotnet backed <jsonValueType> <value>
	When I try to get the property <propertyName> using <propertyNameType>
	Then the property should <propertyFound>
	And the property value should be <propertyValue>

	Examples:
		| jsonValueType | propertyName | propertyValue | value          | propertyNameType   | propertyFound |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | string             | be found      |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonObject    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonAny       | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonNotAny    | foo          | "bar"         | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonObject    | foo          | undefined     | {}             | string             | not be found  |
		| JsonAny       | foo          | undefined     | {}             | string             | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | string             | not be found  |
		| JsonObject    | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonAny       | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | ReadOnlySpan<char> | not be found  |
		| JsonObject    | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | foo          | undefined     | {}             | ReadOnlySpan<byte> | not be found  |

Scenario Outline: Check for the existence of properties for a JsonElement backed JsonProperty
	Given the JsonElement backed <jsonValueType> <value>
	When I check the existence of the property <propertyName> using <propertyNameType>
	Then the property should <propertyFound>

	Examples:
		| jsonValueType | propertyName | value          | propertyNameType   | propertyFound |
		| JsonObject    | foo          | {"foo": "bar"} | string             | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | string             | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | string             | be found      |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonObject    | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonObject    | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonObject    | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonObject    | foo          | {}             | string             | not be found  |
		| JsonAny       | foo          | {}             | string             | not be found  |
		| JsonNotAny    | foo          | {}             | string             | not be found  |
		| JsonObject    | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonAny       | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonObject    | foo          | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | foo          | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | foo          | {}             | ReadOnlySpan<byte> | not be found  |

Scenario Outline: Check for the existence of properties for a dotnet backed JsonProperty
	Given the dotnet backed <jsonValueType> <value>
	When I check the existence of the property <propertyName> using <propertyNameType>
	Then the property should <propertyFound>

	Examples:
		| jsonValueType | propertyName | value          | propertyNameType   | propertyFound |
		| JsonObject    | foo          | {"foo": "bar"} | string             | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | string             | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | string             | be found      |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<char> | be found      |
		| JsonObject    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonAny       | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonNotAny    | foo          | {"foo": "bar"} | ReadOnlySpan<byte> | be found      |
		| JsonObject    | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | string             | not be found  |
		| JsonObject    | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | ReadOnlySpan<char> | not be found  |
		| JsonObject    | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | bar          | {"foo": "bar"} | ReadOnlySpan<byte> | not be found  |
		| JsonObject    | foo          | {}             | string             | not be found  |
		| JsonAny       | foo          | {}             | string             | not be found  |
		| JsonNotAny    | foo          | {}             | string             | not be found  |
		| JsonObject    | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonAny       | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonNotAny    | foo          | {}             | ReadOnlySpan<char> | not be found  |
		| JsonObject    | foo          | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonAny       | foo          | {}             | ReadOnlySpan<byte> | not be found  |
		| JsonNotAny    | foo          | {}             | ReadOnlySpan<byte> | not be found  |