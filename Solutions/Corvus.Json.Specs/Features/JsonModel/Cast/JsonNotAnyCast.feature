Feature: JsonNotAnyCast
	Validate the Json cast operators

Scenario: Cast to dictionary for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny {"foo": 3}
	When I cast the JsonNotAny to ImmutableDictionary<JsonPropertyName,JsonAny>
	Then the result should equal the ImmutableDictionary<JsonPropertyName,JsonAny> '{"foo": 3}'

Scenario: Cast to dictionary for dotnet backed value as a JsonNotAny
	Given the object backed JsonNotAny {"foo": 3}
	When I cast the JsonNotAny to ImmutableDictionary<JsonPropertyName,JsonAny>
	Then the result should equal the ImmutableDictionary<JsonPropertyName,JsonAny> '{"foo": 3}'

Scenario: Cast from dictionary for json element backed value as a JsonNotAny
	Given the ImmutableDictionary<JsonPropertyName,JsonAny> for {"foo": 3}
	When I cast the ImmutableDictionary<JsonPropertyName,JsonAny> to JsonNotAny
	Then the result should equal the JsonNotAny '{"foo": 3}'

Scenario: Cast to JsonString for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny "hello@endjin.com"
	When I cast the JsonNotAny to JsonString
	Then the result should equal the JsonString 'hello@endjin.com'

Scenario: Cast to JsonString for dotnet backed value as a JsonNotAny
	Given the string backed JsonNotAny hello@endjin.com
	When I cast the JsonNotAny to JsonString
	Then the result should equal the JsonString 'hello@endjin.com'

Scenario: Cast from JsonString for json element backed value as a JsonNotAny
	Given the JsonString for "hello@endjin.com"
	When I cast the JsonString to JsonNotAny
	Then the result should equal the JsonNotAny 'hello@endjin.com'

Scenario: Cast from JsonString for dotnet backed value as a JsonNotAny
	Given the dotnet backed JsonString hello@endjin.com
	When I cast the JsonString to JsonNotAny
	Then the result should equal the JsonNotAny 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<byte> for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny "hello@endjin.com"
	When I cast the JsonNotAny to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<byte> for dotnet backed value as a string
	Given the string backed JsonNotAny hello@endjin.com
	When I cast the JsonNotAny to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'hello@endjin.com'

Scenario: Cast from ReadOnlySpan<byte> for json element backed value as a JsonNotAny
	Given the ReadOnlyMemory<byte> for "hello@endjin.com"
	When I cast the ReadOnlySpan<byte> to JsonNotAny
	Then the result should equal the JsonNotAny 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny "hello@endjin.com"
	When I cast the JsonNotAny to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a JsonNotAny
	Given the string backed JsonNotAny hello@endjin.com
	When I cast the JsonNotAny to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'hello@endjin.com'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a JsonNotAny
	Given the ReadOnlyMemory<char> for "hello@endjin.com"
	When I cast the ReadOnlySpan<char> to JsonNotAny
	Then the result should equal the JsonNotAny 'hello@endjin.com'

Scenario: Cast to string for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny "hello@endjin.com"
	When I cast the JsonNotAny to string
	Then the result should equal the string 'hello@endjin.com'

Scenario: Cast to string for dotnet backed value as a JsonNotAny
	Given the string backed JsonNotAny hello@endjin.com
	When I cast the JsonNotAny to string
	Then the result should equal the string 'hello@endjin.com'

Scenario: Cast from string for json element backed value as a JsonNotAny
	Given the string for "hello@endjin.com"
	When I cast the string to JsonNotAny
	Then the result should equal the JsonNotAny 'hello@endjin.com'

Scenario: Cast to bool for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny true
	When I cast the JsonNotAny to bool
	Then the result should equal the bool true

Scenario: Cast to bool for dotnet backed value as a JsonNotAny
	Given the boolean backed JsonNotAny true
	When I cast the JsonNotAny to bool
	Then the result should equal the bool true

Scenario: Cast from bool for json element backed value as a JsonNotAny
	Given the bool for true
	When I cast the bool to JsonNotAny
	Then the result should equal the JsonNotAny 'true'

Scenario: Cast to JsonBoolean for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny true
	When I cast the JsonNotAny to JsonBoolean
	Then the result should equal the JsonBoolean 'true'

Scenario: Cast to JsonBoolean for dotnet backed value as a JsonNotAny
	Given the boolean backed JsonNotAny true
	When I cast the JsonNotAny to JsonBoolean
	Then the result should equal the JsonBoolean 'true'

Scenario: Cast from JsonBoolean for dotnet backed value as a JsonNotAny
	Given the dotnet backed JsonBoolean true
	When I cast the JsonBoolean to JsonNotAny
	Then the result should equal the JsonNotAny 'true'

Scenario: Cast from JsonBoolean for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonBoolean true
	When I cast the JsonBoolean to JsonNotAny
	Then the result should equal the JsonNotAny 'true'

Scenario: Cast to JsonNumber for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny 1.2
	When I cast the JsonNotAny to JsonNumber
	Then the result should equal the JsonNumber '1.2'

Scenario: Cast to JsonNumber for dotnet backed value as a JsonNotAny
	Given the number backed JsonNotAny 1.2
	When I cast the JsonNotAny to JsonNumber
	Then the result should equal the JsonNumber '1.2'

Scenario: Cast from JsonNumber for dotnet backed value as a JsonNotAny
	Given the dotnet backed JsonNumber 1.2
	When I cast the JsonNumber to JsonNotAny
	Then the result should equal the JsonNotAny '1.2' within 0.00001

	Scenario: Cast from JsonNumber for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNumber 1.2
	When I cast the JsonNumber to JsonNotAny
	Then the result should equal the JsonNotAny '1.2' within 0.00001

Scenario: Cast to JsonObject for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny {"foo": "bar"}
	When I cast the JsonNotAny to JsonObject
	Then the result should equal the JsonObject '{"foo": "bar"}'

Scenario: Cast to JsonObject for dotnet backed value as a JsonNotAny
	Given the object backed JsonNotAny {"foo": "bar"}
	When I cast the JsonNotAny to JsonObject
	Then the result should equal the JsonObject '{"foo": "bar"}'

Scenario: Cast from JsonObject for dotnet backed value as a JsonNotAny
	Given the dotnet backed JsonObject {"foo": "bar"}
	When I cast the JsonObject to JsonNotAny
	Then the result should equal the JsonNotAny '{"foo": "bar"}'

Scenario: Cast from JsonObject for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonObject {"foo": "bar"}
	When I cast the JsonObject to JsonNotAny
	Then the result should equal the JsonNotAny '{"foo": "bar"}'

Scenario: Cast to JsonArray for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny [1,2,"3", 4]
	When I cast the JsonNotAny to JsonArray
	Then the result should equal the JsonArray '[1,2,"3", 4]'

Scenario: Cast to JsonArray for dotnet backed value as a JsonNotAny
	Given the array backed JsonNotAny [1,2,"3", 4]
	When I cast the JsonNotAny to JsonArray
	Then the result should equal the JsonArray '[1,2,"3", 4]'

Scenario: Cast from JsonArray for dotnet backed value as a JsonNotAny
	Given the dotnet backed JsonArray [1,2,"3", 4]
	When I cast the JsonArray to JsonNotAny
	Then the result should equal the JsonNotAny '[1,2,"3", 4]'

Scenario: Cast from JsonArray for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonArray [1,2,"3", 4]
	When I cast the JsonArray to JsonNotAny
	Then the result should equal the JsonNotAny '[1,2,"3", 4]'

Scenario: Cast to long for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny 12
	When I cast the JsonNotAny to long
	Then the result should equal the long 12

Scenario: Cast to long for dotnet backed value as a JsonNotAny
	Given the number backed JsonNotAny 12
	When I cast the JsonNotAny to long
	Then the result should equal the long 12

Scenario: Cast from long for json element backed value as a JsonNotAny
	Given the long for 12
	When I cast the long to JsonNotAny
	Then the result should equal the JsonNotAny '12'

Scenario: Cast to double for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny 1.2
	When I cast the JsonNotAny to double
	Then the result should equal the double 1.2

Scenario: Cast to double for dotnet backed value as a JsonNotAny
	Given the number backed JsonNotAny 1.2
	When I cast the JsonNotAny to double
	Then the result should equal the double 1.2

Scenario: Cast from double for json element backed value as a JsonNotAny
	Given the double for 1.2
	When I cast the double to JsonNotAny
	Then the result should equal the JsonNotAny '1.2'

Scenario: Cast to int for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny 12
	When I cast the JsonNotAny to int
	Then the result should equal the int 12

Scenario: Cast to int for dotnet backed value as a JsonNotAny
	Given the number backed JsonNotAny 12
	When I cast the JsonNotAny to int
	Then the result should equal the int 12

Scenario: Cast from int for json element backed value as a JsonNotAny
	Given the int for 12
	When I cast the int to JsonNotAny
	Then the result should equal the JsonNotAny '12'

Scenario: Cast to float for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny 1.2
	When I cast the JsonNotAny to float
	Then the result should equal the float 1.2

Scenario: Cast to float for dotnet backed value as a number
	Given the number backed JsonNotAny 1.2
	When I cast the JsonNotAny to float
	Then the result should equal the float 1.2

Scenario: Cast from float for json element backed value as a JsonNotAny
	Given the float for 1.2
	When I cast the float to JsonNotAny
	Then the result should equal the JsonNotAny '1.2' within 0.00001

Scenario: Cast to ImmutableList for json element backed value as a JsonNotAny
	Given the JsonElement backed JsonNotAny [1,"2",3]
	When I cast the JsonNotAny to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> '[1, "2", 3]'

Scenario: Cast to ImmutableList for dotnet backed value as a JsonNotAny
	Given the array backed JsonNotAny [1,"2",3]
	When I cast the JsonNotAny to ImmutableList<JsonAny>
	Then the result should equal the ImmutableList<JsonAny> '[1, "2", 3]'

Scenario: Cast from ImmutableList for json element backed value as a JsonNotAny
	Given the ImmutableList<JsonAny> for [1,"2",3]
	When I cast the ImmutableList<JsonAny> to JsonNotAny
	Then the result should equal the JsonNotAny '[1, "2", 3]'