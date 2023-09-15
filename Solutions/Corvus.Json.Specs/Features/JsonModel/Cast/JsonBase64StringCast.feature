Feature: JsonBase64StringCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an base64String
	Given the JsonElement backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonAny for dotnet backed value as an base64String
	Given the dotnet backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonString for json element backed value as an base64String
	Given the JsonElement backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonString for dotnet backed value as an base64String
	Given the dotnet backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast from JsonString for json element backed value as an base64String
	Given the JsonString for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonString to JsonBase64String
	Then the result should equal the JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an base64String
	Given the JsonElement backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an base64String
	Given the dotnet backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an base64String
	Given the ReadOnlyMemory<char> for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the ReadOnlySpan<char> to JsonBase64String
	Then the result should equal the JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to string for json element backed value as an base64String
	Given the JsonElement backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to string for dotnet backed value as an base64String
	Given the dotnet backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64String to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast from string for json element backed value as an base64String
	Given the string for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the string to JsonBase64String
	Then the result should equal the JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="

