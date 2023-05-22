Feature: JsonStringTryGetValue
	Optimize parsing a value from a JSON string

Scenario: Get a numeric value from a dotnet-backed string using a char parser
	Given the dotnet backed JsonString "2"
	When you try get an integer from the json value using a char parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

Scenario: Get a numeric value from a jsonelement-backed string using a char parser
	Given the JsonElement backed JsonString "2"
	When you try get an integer from the json value using a char parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

Scenario: Get a numeric value from a dotnet-backed string which does not support the format using a char parser
	Given the dotnet backed JsonString "Hello"
	When you try get an integer from the json value using a char parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

Scenario: Get a numeric value from a jsonelement-backed string which does not support the format using a char parser
	Given the JsonElement backed JsonString "Hello"
	When you try get an integer from the json value using a char parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

Scenario: Get a numeric value from a dotnet-backed string using a utf8 parser
	Given the dotnet backed JsonString "2"
	When you try get an integer from the json value using a utf8 parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

Scenario: Get a numeric value from a jsonelement-backed string using a utf8 parser
	Given the JsonElement backed JsonString "2"
	When you try get an integer from the json value using a utf8 parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

Scenario: Get a numeric value from a dotnet-backed string which does not support the format using a utf8 parser
	Given the dotnet backed JsonString "Hello"
	When you try get an integer from the json value using a utf8 parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

Scenario: Get a numeric value from a jsonelement-backed string which does not support the format using a utf8 parser
	Given the JsonElement backed JsonString "Hello"
	When you try get an integer from the json value using a utf8 parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null