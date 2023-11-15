Feature: JsonRegexCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a regex
	Given the JsonElement backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to JsonAny
	Then the result should equal the JsonAny "([abc])+\\s+$"

Scenario: Cast to JsonAny for dotnet backed value as a regex
	Given the dotnet backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to JsonAny
	Then the result should equal the JsonAny "([abc])+\\s+$"

Scenario: Cast to JsonString for json element backed value as a regex
	Given the JsonElement backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to JsonString
	Then the result should equal the JsonString "([abc])+\\s+$"

Scenario: Cast to JsonString for dotnet backed value as a regex
	Given the dotnet backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to JsonString
	Then the result should equal the JsonString "([abc])+\\s+$"

Scenario: Cast from JsonString for json element backed value as a regex
	Given the JsonString for "([abc])+\\s+$"
	When I cast the JsonString to JsonRegex
	Then the result should equal the JsonRegex "([abc])+\\s+$"

Scenario: Cast to Regex for json element backed value as a regex
	Given the JsonElement backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to Regex
	Then the result should equal the Regex ([abc])+\s+$

Scenario: Cast to Regex for dotnet backed value as a regex
	Given the dotnet backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to Regex
	Then the result should equal the Regex ([abc])+\s+$

Scenario: Cast from Regex for json element backed value as a regex
	Given the Regex for "([abc])+\s+$"
	When I cast the Regex to JsonRegex
	Then the result should equal the JsonRegex "([abc])+\\s+$"

Scenario: Cast to string for json element backed value as a regex
	Given the JsonElement backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to string
	Then the result should equal the string "([abc])+\s+$"

Scenario: Cast to string for dotnet backed value as a regex
	Given the dotnet backed JsonRegex "([abc])+\\s+$"
	When I cast the JsonRegex to string
	Then the result should equal the string "([abc])+\s+$"

Scenario: Cast from string for json element backed value as a regex
	Given the string for "([abc])+\s+$"
	When I cast the string to JsonRegex
	Then the result should equal the JsonRegex "([abc])+\\s+$"

