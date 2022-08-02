Feature: JsonDateCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an date
	Given the JsonElement backed JsonDate "2018-11-13"
	When I cast the JsonDate to JsonAny
	Then the result should equal the JsonAny "2018-11-13"

Scenario: Cast to JsonAny for dotnet backed value as an date
	Given the dotnet backed JsonDate "2018-11-13"
	When I cast the JsonDate to JsonAny
	Then the result should equal the JsonAny "2018-11-13"

Scenario: Cast from JsonAny for json element backed value as an date
	Given the JsonAny for "2018-11-13"
	When I cast the JsonAny to JsonDate
	Then the result should equal the JsonDate "2018-11-13"

Scenario: Cast to JsonString for json element backed value as an date
	Given the JsonElement backed JsonDate "2018-11-13"
	When I cast the JsonDate to JsonString
	Then the result should equal the JsonString "2018-11-13"

Scenario: Cast to JsonString for dotnet backed value as an date
	Given the dotnet backed JsonDate "2018-11-13"
	When I cast the JsonDate to JsonString
	Then the result should equal the JsonString "2018-11-13"

Scenario: Cast from JsonString for json element backed value as an date
	Given the JsonString for "2018-11-13"
	When I cast the JsonString to JsonDate
	Then the result should equal the JsonDate "2018-11-13"

Scenario: Cast from LocalDate for json element backed value as an date
	Given the JsonElement backed JsonDate "2018-11-13"
	When I cast the JsonDate to LocalDate
	Then the result should equal the LocalDate "2018-11-13"

Scenario: Cast to LocalDate for json element backed value as an date
	Given the LocalDate for "2018-11-13"
	When I cast the LocalDate to JsonDate
	Then the result should equal the JsonDate "2018-11-13"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an date
	Given the JsonElement backed JsonDate "2018-11-13"
	When I cast the JsonDate to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "2018-11-13"

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an date
	Given the dotnet backed JsonDate "2018-11-13"
	When I cast the JsonDate to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "2018-11-13"

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an date
	Given the ReadOnlyMemory<char> for "2018-11-13"
	When I cast the ReadOnlySpan<char> to JsonDate
	Then the result should equal the JsonDate "2018-11-13"

Scenario: Cast to string for json element backed value as an date
	Given the JsonElement backed JsonDate "2018-11-13"
	When I cast the JsonDate to string
	Then the result should equal the string "2018-11-13"

Scenario: Cast to string for dotnet backed value as an date
	Given the dotnet backed JsonDate "2018-11-13"
	When I cast the JsonDate to string
	Then the result should equal the string "2018-11-13"

Scenario: Cast from string for json element backed value as an date
	Given the string for "2018-11-13"
	When I cast the string to JsonDate
	Then the result should equal the JsonDate "2018-11-13"

