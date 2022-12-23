Feature: JsonBooleanCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a boolean
	Given the JsonElement backed JsonBoolean true
	When I cast the JsonBoolean to JsonAny
	Then the result should equal the JsonAny true

Scenario: Cast to JsonAny for dotnet backed value as a boolean
	Given the dotnet backed JsonBoolean true
	When I cast the JsonBoolean to JsonAny
	Then the result should equal the JsonAny true

Scenario: Cast from JsonAny for json element backed value as a boolean
	Given the JsonAny for true
	When I cast the JsonAny to JsonBoolean
	Then the result should equal the JsonBoolean true

Scenario: Cast to bool for json element backed value as a boolean
	Given the JsonElement backed JsonBoolean true
	When I cast the JsonBoolean to bool
	Then the result should equal the bool true

Scenario: Cast to bool for dotnet backed value as a boolean
	Given the dotnet backed JsonBoolean true
	When I cast the JsonBoolean to bool
	Then the result should equal the bool true

Scenario: Cast from bool for json element backed value as a boolean
	Given the bool for true
	When I cast the bool to JsonBoolean
	Then the result should equal the JsonBoolean true