Feature: JsonObjectCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an object
	Given the JsonElement backed JsonObject {"foo": 3}
	When I cast the JsonObject to JsonAny
	Then the result should equal the JsonAny '{"foo": 3}'

Scenario: Cast to JsonAny for dotnet backed value as an object
	Given the dotnet backed JsonObject {"foo": 3}
	When I cast the JsonObject to JsonAny
	Then the result should equal the JsonAny '{"foo": 3}'

Scenario: Cast from JsonAny for json element backed value as an object
	Given the JsonAny for {"foo": 3}
	When I cast the JsonAny to JsonObject
	Then the result should equal the JsonObject '{"foo": 3}'

Scenario: Cast to dictionary for json element backed value as an object
	Given the JsonElement backed JsonObject {"foo": 3}
	When I cast the JsonObject to ImmutableDictionary<string,JsonAny>
	Then the result should equal the ImmutableDictionary<string,JsonAny> '{"foo": 3}'

Scenario: Cast to dictionary for dotnet backed value as an object
	Given the dotnet backed JsonObject {"foo": 3}
	When I cast the JsonObject to ImmutableDictionary<string,JsonAny>
	Then the result should equal the ImmutableDictionary<string,JsonAny> '{"foo": 3}'

Scenario: Cast from dictionary for json element backed value as an object
	Given the ImmutableDictionary<string,JsonAny> for {"foo": 3}
	When I cast the ImmutableDictionary<string,JsonAny> to JsonObject
	Then the result should equal the JsonObject '{"foo": 3}'