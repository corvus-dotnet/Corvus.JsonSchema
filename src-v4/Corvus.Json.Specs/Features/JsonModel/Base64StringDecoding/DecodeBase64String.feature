Feature: Decode base64 string

Scenario Outline: Decode a valid string
Given the <Backing> backed JsonBase64String "SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc="
When I get the decoded value for the JsonBase64String with a buffer size of 1024
Then the decoded value should be the UTF8 bytes for 'I have encoded this string'
Examples:
| Backing     |
| JsonElement |
| dotnet      |

Scenario Outline: Decode a valid string with a buffer that is too short
Given the <Backing> backed JsonBase64String "SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc="
When I get the decoded value for the JsonBase64String with a buffer size of 10
Then the decoded byte count should be greater than or equal to 26
Examples:
| Backing     |
| JsonElement |
| dotnet      |

Scenario Outline: Decode an invalid base64 string
Given the <Backing> backed JsonBase64String "SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc="
When I get the decoded value for the JsonBase64String with a buffer size of 1024
Then the decoded byte count should be 0
Examples:
| Backing     |
| JsonElement |
| dotnet      |


Scenario Outline: Test validity of a valid base64 string
Given the <Backing> backed JsonBase64String "SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc="
Then the JsonBase64String has base64 bytes
Examples:
| Backing     |
| JsonElement |
| dotnet      |

Scenario Outline: Test validity of an invalid base64 string
Given the <Backing> backed JsonBase64String "SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc="
Then the JsonBase64String does not have base64 bytes
Examples:
| Backing     |
| JsonElement |
| dotnet      |