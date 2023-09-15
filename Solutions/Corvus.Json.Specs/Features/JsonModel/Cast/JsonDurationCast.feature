Feature: JsonDurationCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to JsonAny
	Then the result should equal the JsonAny "P3Y6M4DT12H30M5S"

Scenario: Cast to JsonAny for dotnet backed value as a duration
	Given the dotnet backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to JsonAny
	Then the result should equal the JsonAny "P3Y6M4DT12H30M5S"

Scenario: Cast to JsonString for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to JsonString
	Then the result should equal the JsonString "P3Y6M4DT12H30M5S"

Scenario: Cast to JsonString for dotnet backed value as a duration
	Given the dotnet backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to JsonString
	Then the result should equal the JsonString "P3Y6M4DT12H30M5S"

Scenario: Cast from JsonString for json element backed value as a duration
	Given the JsonString for "P3Y6M4DT12H30M5S"
	When I cast the JsonString to JsonDuration
	Then the result should equal the JsonDuration "P3Y6M4DT12H30M5S"

Scenario: Cast from Period for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to Period
	Then the result should equal the Period "P3Y6M4DT12H30M5S"

Scenario: Cast to Period for json element backed value as a duration
	Given the Period for "P3Y6M4DT12H30M5S"
	When I cast the Period to JsonDuration
	Then the result should equal the JsonDuration "P3Y6M4DT12H30M5S"

Scenario: Cast from Corvus Period for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to Corvus Period
	Then the result should equal the Corvus Period "P3Y6M4DT12H30M5S"

Scenario: Cast to Corvus Period for json element backed value as a duration
	Given the Corvus Period for "P3Y6M4DT12H30M5S"
	When I cast the Corvus Period to JsonDuration
	Then the result should equal the JsonDuration "P3Y6M4DT12H30M5S"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "P3Y6M4DT12H30M5S"

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a duration
	Given the dotnet backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "P3Y6M4DT12H30M5S"

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a duration
	Given the ReadOnlyMemory<char> for "P3Y6M4DT12H30M5S"
	When I cast the ReadOnlySpan<char> to JsonDuration
	Then the result should equal the JsonDuration "P3Y6M4DT12H30M5S"

Scenario: Cast to string for json element backed value as a duration
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to string
	Then the result should equal the string "P3Y6M4DT12H30M5S"

Scenario: Cast to string for dotnet backed value as a duration
	Given the dotnet backed JsonDuration "P3Y6M4DT12H30M5S"
	When I cast the JsonDuration to string
	Then the result should equal the string "P3Y6M4DT12H30M5S"

Scenario: Cast from string for json element backed value as a duration
	Given the string for "P3Y6M4DT12H30M5S"
	When I cast the string to JsonDuration
	Then the result should equal the JsonDuration "P3Y6M4DT12H30M5S"

