Feature: JsonUriTemplateCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an uriTemplate
	Given the JsonElement backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to JsonAny
	Then the result should equal the JsonAny "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast to JsonAny for dotnet backed value as an uriTemplate
	Given the dotnet backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to JsonAny
	Then the result should equal the JsonAny "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast to JsonString for json element backed value as an uriTemplate
	Given the JsonElement backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to JsonString
	Then the result should equal the JsonString "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast to JsonString for dotnet backed value as an uriTemplate
	Given the dotnet backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to JsonString
	Then the result should equal the JsonString "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast from JsonString for json element backed value as an uriTemplate
	Given the JsonString for "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonString to JsonUriTemplate
	Then the result should equal the JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast to string for json element backed value as an uriTemplate
	Given the JsonElement backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to string
	Then the result should equal the string "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast to string for dotnet backed value as an uriTemplate
	Given the dotnet backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When I cast the JsonUriTemplate to string
	Then the result should equal the string "http://example.com/dictionary/{term:1}/{term}"

Scenario: Cast from string for json element backed value as an uriTemplate
	Given the string for "http://example.com/dictionary/{term:1}/{term}"
	When I cast the string to JsonUriTemplate
	Then the result should equal the JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"

