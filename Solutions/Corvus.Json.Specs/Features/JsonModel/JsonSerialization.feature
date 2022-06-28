Feature: JsonSerialization
	Writing entities

Scenario Outline: Serialize a jsonelement-backed JsonAny to a string
	Given the JsonElement backed JsonAny <jsonValue>
	When the json value is round-trip serialized via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |

Scenario Outline: Serialize a dotnet-backed JsonAny to a string
	Given the dotnet backed JsonAny <jsonValue>
	When the json value is round-trip serialized via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |


Scenario Outline: Write a jsonelement-backed JsonAny to a string
	Given the JsonElement backed JsonAny <jsonValue>
	When the json value is round-tripped via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |

Scenario Outline: Write a dotnet-backed JsonAny to a string
	Given the dotnet backed JsonAny <jsonValue>
	When the json value is round-tripped via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |

Scenario Outline: Write a jsonelement-backed JsonNotAny to a string
	Given the JsonElement backed JsonNotAny <jsonValue>
	When the json value is round-tripped via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |

Scenario Outline: Write a dotnet-backed JsonNotAny to a string
	Given the dotnet backed JsonNotAny <jsonValue>
	When the json value is round-tripped via a string
	Then the round-tripped result should be <type>
	And the round-tripped result should be equal to the JsonAny <jsonValue>

	Examples:
		| jsonValue                               | type      |
		| {"foo": 3, "bar": "hello", "baz": null} | an Object |
		| [1,2,"3",4.0]                           | an Array  |
		| true                                    | a Boolean |
		| "Hello world"                           | a String  |
		| 3.2                                     | a Number  |
		| null                                    | Null      |

Scenario: Write a jsonelement-backed JsonObject to a string
	Given the JsonElement backed JsonObject {"foo": 3, "bar": "hello", "baz": null}
	When the json value is round-tripped via a string
	Then the round-tripped result should be an Object
	And the round-tripped result should be equal to the JsonAny {"foo": 3, "bar": "hello", "baz": null}

Scenario: Write a dotnet-backed JsonObject to a string
	Given the dotnet backed JsonObject {"foo": 3, "bar": "hello", "baz": null}
	When the json value is round-tripped via a string
	Then the round-tripped result should be an Object
	And the round-tripped result should be equal to the JsonAny {"foo": 3, "bar": "hello", "baz": null}

Scenario: Write a jsonelement-backed JsonArray to a string
	Given the JsonElement backed JsonArray [1,2,"3",4.0]
	When the json value is round-tripped via a string
	Then the round-tripped result should be an Array
	And the round-tripped result should be equal to the JsonAny [1,2,"3",4.0]

Scenario: Write a dotnet-backed JsonArray to a string
	Given the dotnet backed JsonArray [1,2,"3",4.0]
	When the json value is round-tripped via a string
	Then the round-tripped result should be an Array
	And the round-tripped result should be equal to the JsonAny [1,2,"3",4.0]

Scenario: Write a jsonelement-backed JsonBoolean to a string
	Given the JsonElement backed JsonBoolean true
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Boolean
	And the round-tripped result should be equal to the JsonAny true

Scenario: Write a dotnet-backed JsonBoolean to a string
	Given the dotnet backed JsonBoolean true
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Boolean
	And the round-tripped result should be equal to the JsonAny true

Scenario: Write a jsonelement-backed JsonString to a string
	Given the JsonElement backed JsonString "Hello, World"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "Hello, World"

Scenario: Write a dotnet-backed JsonString to a string
	Given the dotnet backed JsonString "Hello, World"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "Hello, World"

Scenario: Write a jsonelement-backed JsonBase64Content to a string
	Given the JsonElement backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Write a dotnet-backed JsonBase64Content to a string
	Given the dotnet backed JsonBase64Content "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Write a jsonelement-backed JsonBase64String to a string
	Given the JsonElement backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Write a dotnet-backed JsonBase64String to a string
	Given the dotnet backed JsonBase64String "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="

Scenario: Write a jsonelement-backed JsonContent to a string
	Given the JsonElement backed JsonContent "{\"foo\": \"bar\"}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "{\"foo\": \"bar\"}"

Scenario: Write a dotnet-backed JsonContent to a string
	Given the dotnet backed JsonContent "{\"foo\": \"bar\"}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "{\"foo\": \"bar\"}"

Scenario: Write a jsonelement-backed JsonDate to a string
	Given the JsonElement backed JsonDate "2018-11-13"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "2018-11-13"

Scenario: Write a dotnet-backed JsonDate to a string
	Given the dotnet backed JsonDate "2018-11-13"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "2018-11-13"

Scenario: Write a jsonelement-backed JsonDateTime to a string
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "2018-11-13T20:20:39+00:00"

Scenario: Write a dotnet-backed JsonDateTime to a string
	Given the dotnet backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "2018-11-13T20:20:39+00:00"

Scenario: Write a jsonelement-backed JsonDuration to a string
	Given the JsonElement backed JsonDuration "P3Y6M4DT12H30M5S"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "P3Y6M4DT12H30M5S"

Scenario: Write a dotnet-backed JsonDuration to a string
	Given the dotnet backed JsonDuration "P3Y6M4DT12H30M5S"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "P3Y6M4DT12H30M5S"

Scenario: Write a jsonelement-backed JsonEmail to a string
	Given the JsonElement backed JsonEmail "hello@endjin.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "hello@endjin.com"

Scenario: Write a dotnet-backed JsonEmail to a string
	Given the dotnet backed JsonEmail "hello@endjin.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "hello@endjin.com"

Scenario: Write a jsonelement-backed JsonHostname to a string
	Given the JsonElement backed JsonHostname "www.example.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "www.example.com"

Scenario: Write a dotnet-backed JsonHostname to a string
	Given the dotnet backed JsonHostname "www.example.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "www.example.com"

Scenario: Write a jsonelement-backed JsonIdnEmail to a string
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "hello@endjin.com"

Scenario: Write a dotnet-backed JsonIdnEmail to a string
	Given the dotnet backed JsonIdnEmail "hello@endjin.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "hello@endjin.com"

Scenario: Write a jsonelement-backed JsonIdnHostname to a string
	Given the JsonElement backed JsonIdnHostname "www.example.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "www.example.com"

Scenario: Write a dotnet-backed JsonIdnHostname to a string
	Given the dotnet backed JsonIdnHostname "www.example.com"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "www.example.com"

Scenario: Write a jsonelement-backed JsonInteger to a string
	Given the JsonElement backed JsonInteger 3
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Number
	And the round-tripped result should be equal to the JsonAny 3

Scenario: Write a dotnet-backed JsonInteger to a string
	Given the dotnet backed JsonInteger 3
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Number
	And the round-tripped result should be equal to the JsonAny 3

Scenario: Write a jsonelement-backed JsonIpV4 to a string
	Given the JsonElement backed JsonIpV4 "192.168.0.1"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "192.168.0.1"

Scenario: Write a dotnet-backed JsonIpV4 to a string
	Given the dotnet backed JsonIpV4 "192.168.0.1"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "192.168.0.1"

Scenario: Write a jsonelement-backed JsonIpV6 to a string
	Given the JsonElement backed JsonIpV6 "::ffff:192.168.0.1"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "::ffff:192.168.0.1"

Scenario: Write a dotnet-backed JsonIpV6 to a string
	Given the dotnet backed JsonIpV6 "::ffff:c0a8:0001"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "::ffff:c0a8:0001"

Scenario: Write a jsonelement-backed JsonIri to a string
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a dotnet-backed JsonIri to a string
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a jsonelement-backed JsonIriReference to a string
	Given the JsonElement backed JsonIriReference "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a dotnet-backed JsonIriReference to a string
	Given the dotnet backed JsonIriReference "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a jsonelement-backed JsonNumber to a string
	Given the JsonElement backed JsonNumber 3.2
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Number
	And the round-tripped result should be equal to the JsonAny 3.2

Scenario: Write a dotnet-backed JsonNumber to a string
	Given the dotnet backed JsonNumber 3.2
	When the json value is round-tripped via a string
	Then the round-tripped result should be a Number
	And the round-tripped result should be equal to the JsonAny 3.2

Scenario: Write a jsonelement-backed JsonPointer to a string
	Given the JsonElement backed JsonPointer "0/foo/bar"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "0/foo/bar"

Scenario: Write a dotnet-backed JsonPointer to a string
	Given the dotnet backed JsonPointer "0/foo/bar"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "0/foo/bar"

Scenario: Write a jsonelement-backed JsonRegex to a string
	Given the JsonElement backed JsonRegex "([abc])+\\s+$"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "([abc])+\\s+$"

Scenario: Write a dotnet-backed JsonRegex to a string
	Given the dotnet backed JsonRegex "([abc])+\\s+$"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "([abc])+\\s+$"

Scenario: Write a jsonelement-backed JsonRelativePointer to a string
	Given the JsonElement backed JsonRelativePointer "/a~1b"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "/a~1b"

Scenario: Write a dotnet-backed JsonRelativePointer to a string
	Given the dotnet backed JsonRelativePointer "/a~1b"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "/a~1b"

Scenario: Write a jsonelement-backed JsonTime to a string
	Given the JsonElement backed JsonTime "08:30:06+00:20"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "08:30:06+00:20"

Scenario: Write a dotnet-backed JsonTime to a string
	Given the dotnet backed JsonTime "08:30:06+00:20"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "08:30:06+00:20"

Scenario: Write a jsonelement-backed JsonUri to a string
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a dotnet-backed JsonUri to a string
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a jsonelement-backed JsonUriReference to a string
	Given the JsonElement backed JsonUriReference "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a dotnet-backed JsonUriReference to a string
	Given the dotnet backed JsonUriReference "http://foo.bar/?baz=qux#quux"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Write a jsonelement-backed JsonUriTemplate to a string
	Given the JsonElement backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://example.com/dictionary/{term:1}/{term}"

Scenario: Write a dotnet-backed JsonUriTemplate to a string
	Given the dotnet backed JsonUriTemplate "http://example.com/dictionary/{term:1}/{term}"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "http://example.com/dictionary/{term:1}/{term}"

Scenario: Write a jsonelement-backed JsonUuid to a string
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"

Scenario: Write a dotnet-backed JsonUuid to a string
	Given the dotnet backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When the json value is round-tripped via a string
	Then the round-tripped result should be a String
	And the round-tripped result should be equal to the JsonAny "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"