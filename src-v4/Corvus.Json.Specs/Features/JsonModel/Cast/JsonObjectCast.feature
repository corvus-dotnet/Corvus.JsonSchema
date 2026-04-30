Feature: JsonObjectCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an object
	Given the JsonElement backed JsonObject {"foo": 3}
	When I cast the JsonObject to JsonAny
	Then the result should equal the JsonAny {"foo": 3}

Scenario: Cast to JsonAny for dotnet backed value as an object
	Given the dotnet backed JsonObject {"foo": 3}
	When I cast the JsonObject to JsonAny
	Then the result should equal the JsonAny {"foo": 3}