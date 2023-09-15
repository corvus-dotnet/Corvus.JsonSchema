Feature: JsonBase64ContentCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an base64Content
	Given the JsonElement backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonAny for dotnet backed value as an base64Content
	Given the dotnet backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonString for json element backed value as an base64Content
	Given the JsonElement backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to JsonString for dotnet backed value as an base64Content
	Given the dotnet backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast from JsonString for json element backed value as an base64Content
	Given the JsonString for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonString to JsonBase64Content
	Then the result should equal the JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to string for json element backed value as an base64Content
	Given the JsonElement backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast to string for dotnet backed value as an base64Content
	Given the dotnet backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonBase64Content to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Cast from string for json element backed value as an base64Content
	Given the string for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the string to JsonBase64Content
	Then the result should equal the JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="

