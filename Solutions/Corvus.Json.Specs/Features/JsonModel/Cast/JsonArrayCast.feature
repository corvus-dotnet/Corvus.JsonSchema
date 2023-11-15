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