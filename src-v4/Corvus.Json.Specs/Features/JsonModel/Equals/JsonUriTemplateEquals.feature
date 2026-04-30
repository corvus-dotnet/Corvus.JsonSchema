Feature: JsonUriTemplateEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonUriTemplate
Scenario Outline: Equals for json element backed value as a uriTemplate
	Given the JsonElement backed JsonUriTemplate <jsonValue>
	When I compare it to the uriTemplate <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                            | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}"  | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term2}" | false  |
		| null                                            | null                                             | true   |
		| null                                            | "http://example.com/dictionary/{term:1}/{term}"  | false  |

Scenario Outline: Equals for dotnet backed value as a uriTemplate
	Given the dotnet backed JsonUriTemplate <jsonValue>
	When I compare it to the uriTemplate <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                            | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}"  | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term2}" | false  |

Scenario Outline: Equals for uriTemplate json element backed value as an IJsonValue
	Given the JsonElement backed JsonUriTemplate <jsonValue>
	When I compare the uriTemplate to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                           | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "Hello"                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "Goodbye"                                       | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1                                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1.1                                             | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | [1,2,3]                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | { "first": "1" }                                | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | true                                            | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | false                                           | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13T20:20:39+00:00"                     | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13"                                    | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "P3Y6M4DT12H30M5S"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13"                                    | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "hello@endjin.com"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "www.example.com"                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}" | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "eyAiaGVsbG8iOiAid29ybGQiIH0="                  | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "{ \"first\": \"1\" }"                          | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "192.168.0.1"                                   | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "0:0:0:0:0:ffff:c0a8:0001"                      | false  |

Scenario Outline: Equals for uriTemplate dotnet backed value as an IJsonValue
	Given the dotnet backed JsonUriTemplate <jsonValue>
	When I compare the uriTemplate to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                           | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "Hello"                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "Goodbye"                                       | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1                                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1.1                                             | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | [1,2,3]                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | { "first": "1" }                                | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | true                                            | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | false                                           | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13T20:20:39+00:00"                     | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "P3Y6M4DT12H30M5S"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13"                                    | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "P3Y6M4DT12H30M5S"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "hello@endjin.com"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "www.example.com"                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}" | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "eyAiaGVsbG8iOiAid29ybGQiIH0="                  | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "{ \"first\": \"1\" }"                          | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "192.168.0.1"                                   | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "0:0:0:0:0:ffff:c0a8:0001"                      | false  |

Scenario Outline: Equals for uriTemplate json element backed value as an object
	Given the JsonElement backed JsonUriTemplate <jsonValue>
	When I compare the uriTemplate to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                           | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "Hello"                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "Goodbye"                                       | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1                                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1.1                                             | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | [1,2,3]                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | { "first": "1" }                                | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | true                                            | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | false                                           | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13T20:20:39+00:00"                     | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "P3Y6M4DT12H30M5S"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13"                                    | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "hello@endjin.com"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "www.example.com"                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}" | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "eyAiaGVsbG8iOiAid29ybGQiIH0="                  | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "{ \"first\": \"1\" }"                          | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "192.168.0.1"                                   | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "0:0:0:0:0:ffff:c0a8:0001"                      | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | null                                            | false  |
		#| "http://example.com/dictionary/{term:1}/{term}" | <new object()>                                  | false  |

Scenario Outline: Equals for uriTemplate dotnet backed value as an object
	Given the dotnet backed JsonUriTemplate <jsonValue>
	When I compare the uriTemplate to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                       | value                                           | result |
		| "http://example.com/dictionary/{term:1}/{term}" | "Hello"                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "Goodbye"                                       | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1                                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | 1.1                                             | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | [1,2,3]                                         | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | { "first": "1" }                                | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | true                                            | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | false                                           | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13T20:20:39+00:00"                     | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "2018-11-13"                                    | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "P3Y6M4DT12H30M5S"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "hello@endjin.com"                              | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "www.example.com"                               | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "http://example.com/dictionary/{term:1}/{term}" | true   |
		| "http://example.com/dictionary/{term:1}/{term}" | "eyAiaGVsbG8iOiAid29ybGQiIH0="                  | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "{ \"first\": \"1\" }"                          | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "192.168.0.1"                                   | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | "0:0:0:0:0:ffff:c0a8:0001"                      | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | null                                            | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | <null>                                          | false  |
		| "http://example.com/dictionary/{term:1}/{term}" | <undefined>                                     | false  |
		| null                                            | null                                            | true   |
		| null                                            | <null>                                          | true   |
		| null                                            | <undefined>                                     | false  |
		#| "http://example.com/dictionary/{term:1}/{term}" | <new object()>                                  | false  |