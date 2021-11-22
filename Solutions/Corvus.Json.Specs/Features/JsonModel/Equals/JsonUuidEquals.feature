Feature: JsonUuidEquals
	Valiuuid the Json Equals operator, equality overrides and hashcode

# JsonUuid
Scenario Outline: Equals for json element backed value as a uuid
	Given the JsonElement backed JsonUuid <jsonValue>
	When I compare it to the uuid <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "Garbage"                              | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |
		| null                                   | null                                   | true   |
		| null                                   | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false  |

Scenario Outline: Equals for dotnet backed value as a uuid
	Given the dotnet backed JsonUuid <jsonValue>
	When I compare it to the uuid <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "Garbage"                              | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |

Scenario Outline: Equals for uuid json element backed value as an IJsonValue
	Given the JsonElement backed JsonUuid <jsonValue>
	When I compare the uuid to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Hello"                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1                                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1.1                                    | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | [1,2,3]                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | { "first": "1" }                       | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true                                   | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false                                  | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d2" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "P3Y6M4DT12H30M5S"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "Garbage"                              | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "hello@endjin.com"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "www.example.com"                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "http://foo.bar/?baz=qux#quux"         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "eyAiaGVsbG8iOiAid29ybGQiIH0="         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "{ \"first\": \"1\" }"                 | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "192.168.0.1"                          | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "0:0:0:0:0:ffff:c0a8:0001"             | false  |

Scenario Outline: Equals for uuid dotnet backed value as an IJsonValue
	Given the dotnet backed JsonUuid <jsonValue>
	When I compare the uuid to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Hello"                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1                                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1.1                                    | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | [1,2,3]                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | { "first": "1" }                       | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true                                   | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false                                  | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d2" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "P3Y6M4DT12H30M5S"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "Garbage"                              | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d2" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "hello@endjin.com"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "www.example.com"                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "http://foo.bar/?baz=qux#quux"         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "eyAiaGVsbG8iOiAid29ybGQiIH0="         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "{ \"first\": \"1\" }"                 | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "192.168.0.1"                          | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "0:0:0:0:0:ffff:c0a8:0001"             | false  |

Scenario Outline: Equals for uuid json element backed value as an object
	Given the JsonElement backed JsonUuid <jsonValue>
	When I compare the uuid to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Hello"                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1                                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1.1                                    | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | [1,2,3]                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | { "first": "1" }                       | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true                                   | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false                                  | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d2" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "P3Y6M4DT12H30M5S"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "hello@endjin.com"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "www.example.com"                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "http://foo.bar/?baz=qux#quux"         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "eyAiaGVsbG8iOiAid29ybGQiIH0="         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "{ \"first\": \"1\" }"                 | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "192.168.0.1"                          | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "0:0:0:0:0:ffff:c0a8:0001"             | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | <new object()>                         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | null                                   | false  |

Scenario Outline: Equals for uuid dotnet backed value as an object
	Given the dotnet backed JsonUuid <jsonValue>
	When I compare the uuid to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                              | value                                  | result |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Hello"                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "Goodbye"                              | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1                                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | 1.1                                    | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | [1,2,3]                                | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | { "first": "1" }                       | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true                                   | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | false                                  | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d2" | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "P3Y6M4DT12H30M5S"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | true   |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "hello@endjin.com"                     | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "www.example.com"                      | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "http://foo.bar/?baz=qux#quux"         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "eyAiaGVsbG8iOiAid29ybGQiIH0="         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "{ \"first\": \"1\" }"                 | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "192.168.0.1"                          | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | "0:0:0:0:0:ffff:c0a8:0001"             | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | <new object()>                         | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | null                                   | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | <null>                                 | false  |
		| "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1" | <undefined>                            | false  |
		| null                                   | null                                   | true   |
		| null                                   | <null>                                 | true   |
		| null                                   | <undefined>                            | false  |