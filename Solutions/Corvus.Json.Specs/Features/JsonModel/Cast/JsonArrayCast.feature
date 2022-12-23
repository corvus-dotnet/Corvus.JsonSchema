Feature: JsonArrayCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an array
	Given the JsonElement backed JsonArray [1,"2",3]
	When I cast the JsonArray to JsonAny
	Then the result should equal the JsonAny [1, "2", 3]

Scenario: Cast to JsonAny for dotnet backed value as an array
	Given the dotnet backed JsonArray [1,"2",3]
	When I cast the JsonArray to JsonAny
	Then the result should equal the JsonAny [1, "2", 3]

Scenario: Cast from JsonAny for json element backed value as an array
	Given the JsonAny for [1,"2",3]
	When I cast the JsonAny to JsonArray
	Then the result should equal the JsonArray [1, "2", 3]

Scenario: Cast to ImmutableList for json element backed value as an array
	Given the JsonElement backed JsonArray [1,"2",3]
	When I cast the JsonArray to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> [1, "2", 3]

Scenario: Cast to ImmutableList for dotnet backed value as an array
	Given the dotnet backed JsonArray [1,"2",3]
	When I cast the JsonArray to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> [1, "2", 3]

Scenario: Cast from ImmutableList for json element backed value as an array
	Given the ImmutableList<JsonAny> for [1,"2",3]
	When I cast the ImmutableList<JsonAny> to JsonArray
	Then the result should equal the JsonArray [1, "2", 3]