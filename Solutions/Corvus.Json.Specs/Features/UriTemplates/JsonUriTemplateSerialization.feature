Feature: JsonUriTemplateSerialization
	Writing entities

Scenario: Write a jsonelement-backed JsonUriTemplate to a string
	Given the JsonElement backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://example.com/dictionary/{term:1}/{term}"

Scenario: Write a dotnet-backed JsonUriTemplate to a string
	Given the dotnet backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://example.com/dictionary/{term:1}/{term}"
