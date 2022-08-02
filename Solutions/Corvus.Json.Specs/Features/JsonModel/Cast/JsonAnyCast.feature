Feature: JsonAnyCast
	Validate the Json cast operators

Scenario: Cast to dictionary for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny {"foo": 3}
	When I cast the JsonAny to ImmutableDictionary<JsonPropertyName,JsonAny>
	Then the result should equal the ImmutableDictionary<JsonPropertyName,JsonAny> {"foo": 3}

Scenario: Cast to dictionary for dotnet backed value as a JsonAny
	Given the object backed JsonAny {"foo": 3}
	When I cast the JsonAny to ImmutableDictionary<JsonPropertyName,JsonAny>
	Then the result should equal the ImmutableDictionary<JsonPropertyName,JsonAny> {"foo": 3}

Scenario: Cast from dictionary for json element backed value as a JsonAny
	Given the ImmutableDictionary<JsonPropertyName,JsonAny> for {"foo": 3}
	When I cast the ImmutableDictionary<JsonPropertyName,JsonAny> to JsonAny
	Then the result should equal the JsonAny {"foo": 3}

Scenario: Cast to JsonString for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast to JsonString for dotnet backed value as a JsonAny
	Given the string backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast from JsonString for json element backed value as a JsonAny
	Given the JsonString for "hello@endjin.com"
	When I cast the JsonString to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> hello@endjin.com

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a JsonAny
	Given the string backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> hello@endjin.com

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a JsonAny
	Given the ReadOnlyMemory<char> for "hello@endjin.com"
	When I cast the ReadOnlySpan<char> to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to string for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast to string for dotnet backed value as a JsonAny
	Given the string backed JsonAny "hello@endjin.com"
	When I cast the JsonAny to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast from string for json element backed value as a JsonAny
	Given the string for "hello@endjin.com"
	When I cast the string to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to bool for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny true
	When I cast the JsonAny to bool
	Then the result should equal the bool true

Scenario: Cast to bool for dotnet backed value as a JsonAny
	Given the boolean backed JsonAny true
	When I cast the JsonAny to bool
	Then the result should equal the bool true

Scenario: Cast from bool for json element backed value as a JsonAny
	Given the bool for true
	When I cast the bool to JsonAny
	Then the result should equal the JsonAny true

Scenario: Cast to long for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny 12
	When I cast the JsonAny to long
	Then the result should equal the long 12

Scenario: Cast to long for dotnet backed value as a JsonAny
	Given the number backed JsonAny 12
	When I cast the JsonAny to long
	Then the result should equal the long 12

Scenario: Cast from long for json element backed value as a JsonAny
	Given the long for 12
	When I cast the long to JsonAny
	Then the result should equal the JsonAny 12

Scenario: Cast to double for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny 1.2
	When I cast the JsonAny to double
	Then the result should equal the double 1.2

Scenario: Cast to double for dotnet backed value as a JsonAny
	Given the number backed JsonAny 1.2
	When I cast the JsonAny to double
	Then the result should equal the double 1.2

Scenario: Cast from double for json element backed value as a JsonAny
	Given the double for 1.2
	When I cast the double to JsonAny
	Then the result should equal the JsonAny 1.2

Scenario: Cast to int for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny 12
	When I cast the JsonAny to int
	Then the result should equal the int 12

Scenario: Cast to int for dotnet backed value as a JsonAny
	Given the number backed JsonAny 12
	When I cast the JsonAny to int
	Then the result should equal the int 12

Scenario: Cast from int for json element backed value as a JsonAny
	Given the int for 12
	When I cast the int to JsonAny
	Then the result should equal the JsonAny 12

Scenario: Cast to float for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny 1.2
	When I cast the JsonAny to float
	Then the result should equal the float 1.2

Scenario: Cast to float for dotnet backed value as a number
	Given the number backed JsonAny 1.2
	When I cast the JsonAny to float
	Then the result should equal the float 1.2

Scenario: Cast from float for json element backed value as a JsonAny
	Given the float for 1.2
	When I cast the float to JsonAny
	Then the result should equal within 0.00001 the JsonAny 1.2 

Scenario: Cast to ImmutableList for json element backed value as a JsonAny
	Given the JsonElement backed JsonAny [1,"2",3]
	When I cast the JsonAny to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> [1, "2", 3]

Scenario: Cast to ImmutableList for dotnet backed value as a JsonAny
	Given the array backed JsonAny [1,"2",3]
	When I cast the JsonAny to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> [1, "2", 3]

Scenario: Cast from ImmutableList for json element backed value as a JsonAny
	Given the ImmutableList<JsonAny> for [1,"2",3]
	When I cast the ImmutableList<JsonAny> to JsonAny
	Then the result should equal the JsonAny [1, "2", 3]