Feature: JsonTimeCast
	Valitime the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a time
	Given the JsonElement backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to JsonAny
	Then the result should equal the JsonAny "08:30:06+00:20"

Scenario: Cast to JsonAny for dotnet backed value as a time
	Given the dotnet backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to JsonAny
	Then the result should equal the JsonAny "08:30:06+00:20"

Scenario: Cast to JsonString for json element backed value as a time
	Given the JsonElement backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to JsonString
	Then the result should equal the JsonString "08:30:06+00:20"

Scenario: Cast to JsonString for dotnet backed value as a time
	Given the dotnet backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to JsonString
	Then the result should equal the JsonString "08:30:06+00:20"

Scenario: Cast from JsonString for json element backed value as a time
	Given the JsonString for "08:30:06+00:20"
	When I cast the JsonString to JsonTime
	Then the result should equal the JsonTime "08:30:06+00:20"

Scenario: Cast from OffsetTime for json element backed value as a time
	Given the JsonElement backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to OffsetTime
	Then the result should equal the OffsetTime "08:30:06+00:20"

Scenario: Cast to OffsetTime for json element backed value as a time
	Given the OffsetTime for "08:30:06+00:20"
	When I cast the OffsetTime to JsonTime
	Then the result should equal the JsonTime "08:30:06+00:20"

Scenario: Cast to string for json element backed value as a time
	Given the JsonElement backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to string
	Then the result should equal the string "08:30:06+00:20"

Scenario: Cast to string for dotnet backed value as a time
	Given the dotnet backed JsonTime "08:30:06+00:20"
	When I cast the JsonTime to string
	Then the result should equal the string "08:30:06+00:20"

Scenario: Cast from string for json element backed value as a time
	Given the string for "08:30:06+00:20"
	When I cast the string to JsonTime
	Then the result should equal the JsonTime "08:30:06+00:20"

