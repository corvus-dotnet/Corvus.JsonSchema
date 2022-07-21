Feature: JsonNotAnyEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonNotAny
Scenario Outline: Equals for json element backed value as a notAny
	Given the JsonElement backed JsonNotAny <jsonValue>
	When I compare it to the notAny <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value                           | result |
		| [1,"2",3]        | [1,"2",3]                       | true   |
		| [1,"2",3]        | [3,"2",1]                       | false  |
		| [1,"2",3]        | [1,2,3]                         | false  |
		| []               | []                              | true   |
		| []               | [3,2,1]                         | false  |
		| true             | true                            | true   |
		| false            | false                           | true   |
		| true             | false                           | false  |
		| false            | true                            | false  |
		| true             | null                            | false  |
		| 1                | 1                               | true   |
		| 1.1              | 1.1                             | true   |
		| 1.1              | 1                               | false  |
		| 1.1              | 3                               | false  |
		| { "first": "1" } | { "first": "1" }                | true   |
		| { "first": "1" } | { "first": "2" }                | false  |
		| { "first": "1" } | { "second": "1" }               | false  |
		| { "first": "1" } | { "first": "1", "second": "1" } | false  |
		| { "first": "1" } | { "first": 1, "second": "1" }   | false  |
		| "Hello"          | "Hello"                         | true   |
		| "Hello"          | "Goodbye"                       | false  |
		| null             | null                            | true   |
		| null             | [1,2,3]                         | false  |

Scenario Outline: Equals for dotnet backed value as a notAny
	Given the dotnet backed JsonNotAny <jsonValue>
	When I compare it to the notAny <value>
	Then the result should be <result>

	Examples:
		| jsonValue                                              | value                                                  | result |
		| [1,"2",3]                                              | [1,"2",3]                                              | true   |
		| [1,"2",3]                                              | [3,"2",1]                                              | false  |
		| [1,"2",3]                                              | [1,2,3]                                                | false  |
		| []                                                     | []                                                     | true   |
		| []                                                     | [3,2,1]                                                | false  |
		| false                                                  | true                                                   | false  |
		| false                                                  | false                                                  | true   |
		| true                                                   | true                                                   | true   |
		| true                                                   | false                                                  | false  |
		| 1                                                      | 1                                                      | true   |
		| 1.1                                                    | 1.1                                                    | true   |
		| 1.1                                                    | 1                                                      | false  |
		| 1                                                      | 3                                                      | false  |
		| { "first": "1" }                                       | { "first": "1" }                                       | true   |
		| { "first": "1", "second": 2, "third": { "first": 1 } } | { "first": "1", "second": 2, "third": { "first": 1 } } | true   |
		| { "first": "1" }                                       | { "first": "2" }                                       | false  |
		| { "first": "1" }                                       | { "second": "1" }                                      | false  |
		| { "first": "1" }                                       | { "first": "1", "second": "1" }                        | false  |
		| { "first": "1" }                                       | { "first": 1, "second": "1" }                          | false  |
		| "Hello"                                                | "Hello"                                                | true   |
		| "Hello"                                                | "Goodbye"                                              | false  |

Scenario Outline: Equals for notAny json element backed value as an IJsonValue
	Given the JsonElement backed JsonNotAny <jsonValue>
	When I compare the notAny to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| [1,2,3]          | [1,2,3]          | true   |
		| true             | true             | true   |
		| false            | false            | true   |
		| 1.1              | 1.1              | true   |
		| { "first": "1" } | { "first": "1" } | true   |
		| "Hello"          | "Hello"          | true   |

Scenario Outline: Equals for notAny dotnet backed value as an IJsonValue
	Given the dotnet backed JsonNotAny <jsonValue>
	When I compare the notAny to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| [1,2,3]          | [1,2,3]          | true   |
		| true             | true             | true   |
		| false            | false            | true   |
		| 1.1              | 1.1              | true   |
		| { "first": "1" } | { "first": "1" } | true   |
		| "Hello"          | "Hello"          | true   |

Scenario Outline: Equals for notAny json element backed value as an object
	Given the JsonElement backed JsonNotAny <jsonValue>
	When I compare the notAny to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| [1,2,3]          | [1,2,3]          | true   |
		| true             | true             | true   |
		| false            | false            | true   |
		| 1.1              | 1.1              | true   |
		| { "first": "1" } | { "first": "1" } | true   |
		| "Hello"          | "Hello"          | true   |

Scenario Outline: Equals for notAny dotnet backed value as an object
	Given the dotnet backed JsonNotAny <jsonValue>
	When I compare the notAny to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| [1,2,3]          | [1,2,3]          | true   |
		| true             | true             | true   |
		| false            | false            | true   |
		| 1.1              | 1.1              | true   |
		| { "first": "1" } | { "first": "1" } | true   |
		| "Hello"          | "Hello"          | true   |
		| [1,2,3]          | <new object()>   | false  |
		| [1,2,3]          | null             | false  |
		| [1,2,3]          | <null>           | false  |
		| [1,2,3]          | <undefined>      | false  |
		| <undefined>      | "Hello"          | false  |
		| null             | null             | true   |
		| null             | <null>           | true   |
		| null             | <undefined>      | false  |