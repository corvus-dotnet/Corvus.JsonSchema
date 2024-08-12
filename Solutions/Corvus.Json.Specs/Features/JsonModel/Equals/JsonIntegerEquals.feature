Feature: JsonIntegerEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonInteger
Scenario Outline: Equals for json element backed value as a integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| null      | null  | true   | JsonInteger |
	| null      | 1     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| null      | null  | true   | JsonInt64   |
	| null      | 1     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| null      | null  | true   | JsonInt32   |
	| null      | 1     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| null      | null  | true   | JsonInt16   |
	| null      | 1     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| null      | null  | true   | JsonSByte   |
	| null      | 1     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| null      | null  | true   | JsonUInt64  |
	| null      | 1     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| null      | null  | true   | JsonUInt32  |
	| null      | 1     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| null      | null  | true   | JsonUInt16  |
	| null      | 1     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |
	| null      | null  | true   | JsonByte    |
	| null      | 1     | false  | JsonByte    |

Scenario Outline: Equals for dotnet backed value as a integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |

Scenario Outline: Equals for dotnet backed value as a BinaryJsonNumber
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the BinaryJsonNumber <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |

Scenario Outline: Equals for JsonElement backed value as a BinaryJsonNumber
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the BinaryJsonNumber <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |

Scenario Outline: Equals for integer json element backed value as an IJsonValue
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result | TargetType  |
	| 1         | "Hello"                        | false  | JsonInteger |
	| 1         | "Goodbye"                      | false  | JsonInteger |
	| 1         | 1                              | true   | JsonInteger |
	| 1         | 1.0                            | true   | JsonInteger |
	| 1         | 1.1                            | false  | JsonInteger |
	| 1         | [1,2,3]                        | false  | JsonInteger |
	| 1         | { "first": "1" }               | false  | JsonInteger |
	| 1         | true                           | false  | JsonInteger |
	| 1         | false                          | false  | JsonInteger |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInteger |
	| 1         | "2018-11-13"                   | false  | JsonInteger |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInteger |
	| 1         | "2018-11-13"                   | false  | JsonInteger |
	| 1         | "hello@endjin.com"             | false  | JsonInteger |
	| 1         | "www.example.com"              | false  | JsonInteger |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInteger |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInteger |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInteger |
	| 1         | "192.168.0.1"                  | false  | JsonInteger |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInteger |
	| 1         | "Hello"                        | false  | JsonInt64   |
	| 1         | "Goodbye"                      | false  | JsonInt64   |
	| 1         | 1                              | true   | JsonInt64   |
	| 1         | 1.0                            | true   | JsonInt64   |
	| 1         | 1.1                            | false  | JsonInt64   |
	| 1         | [1,2,3]                        | false  | JsonInt64   |
	| 1         | { "first": "1" }               | false  | JsonInt64   |
	| 1         | true                           | false  | JsonInt64   |
	| 1         | false                          | false  | JsonInt64   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt64   |
	| 1         | "2018-11-13"                   | false  | JsonInt64   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt64   |
	| 1         | "2018-11-13"                   | false  | JsonInt64   |
	| 1         | "hello@endjin.com"             | false  | JsonInt64   |
	| 1         | "www.example.com"              | false  | JsonInt64   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt64   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt64   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt64   |
	| 1         | "192.168.0.1"                  | false  | JsonInt64   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt64   |
	| 1         | "Hello"                        | false  | JsonInt32   |
	| 1         | "Goodbye"                      | false  | JsonInt32   |
	| 1         | 1                              | true   | JsonInt32   |
	| 1         | 1.0                            | true   | JsonInt32   |
	| 1         | 1.1                            | false  | JsonInt32   |
	| 1         | [1,2,3]                        | false  | JsonInt32   |
	| 1         | { "first": "1" }               | false  | JsonInt32   |
	| 1         | true                           | false  | JsonInt32   |
	| 1         | false                          | false  | JsonInt32   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt32   |
	| 1         | "2018-11-13"                   | false  | JsonInt32   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt32   |
	| 1         | "2018-11-13"                   | false  | JsonInt32   |
	| 1         | "hello@endjin.com"             | false  | JsonInt32   |
	| 1         | "www.example.com"              | false  | JsonInt32   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt32   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt32   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt32   |
	| 1         | "192.168.0.1"                  | false  | JsonInt32   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt32   |
	| 1         | "Hello"                        | false  | JsonInt16   |
	| 1         | "Goodbye"                      | false  | JsonInt16   |
	| 1         | 1                              | true   | JsonInt16   |
	| 1         | 1.0                            | true   | JsonInt16   |
	| 1         | 1.1                            | false  | JsonInt16   |
	| 1         | [1,2,3]                        | false  | JsonInt16   |
	| 1         | { "first": "1" }               | false  | JsonInt16   |
	| 1         | true                           | false  | JsonInt16   |
	| 1         | false                          | false  | JsonInt16   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt16   |
	| 1         | "2018-11-13"                   | false  | JsonInt16   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt16   |
	| 1         | "2018-11-13"                   | false  | JsonInt16   |
	| 1         | "hello@endjin.com"             | false  | JsonInt16   |
	| 1         | "www.example.com"              | false  | JsonInt16   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt16   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt16   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt16   |
	| 1         | "192.168.0.1"                  | false  | JsonInt16   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt16   |
	| 1         | "Hello"                        | false  | JsonSByte   |
	| 1         | "Goodbye"                      | false  | JsonSByte   |
	| 1         | 1                              | true   | JsonSByte   |
	| 1         | 1.0                            | true   | JsonSByte   |
	| 1         | 1.1                            | false  | JsonSByte   |
	| 1         | [1,2,3]                        | false  | JsonSByte   |
	| 1         | { "first": "1" }               | false  | JsonSByte   |
	| 1         | true                           | false  | JsonSByte   |
	| 1         | false                          | false  | JsonSByte   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonSByte   |
	| 1         | "2018-11-13"                   | false  | JsonSByte   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonSByte   |
	| 1         | "2018-11-13"                   | false  | JsonSByte   |
	| 1         | "hello@endjin.com"             | false  | JsonSByte   |
	| 1         | "www.example.com"              | false  | JsonSByte   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonSByte   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonSByte   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonSByte   |
	| 1         | "192.168.0.1"                  | false  | JsonSByte   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonSByte   |
	| 1         | "Hello"                        | false  | JsonUInt64  |
	| 1         | "Goodbye"                      | false  | JsonUInt64  |
	| 1         | 1                              | true   | JsonUInt64  |
	| 1         | 1.0                            | true   | JsonUInt64  |
	| 1         | 1.1                            | false  | JsonUInt64  |
	| 1         | [1,2,3]                        | false  | JsonUInt64  |
	| 1         | { "first": "1" }               | false  | JsonUInt64  |
	| 1         | true                           | false  | JsonUInt64  |
	| 1         | false                          | false  | JsonUInt64  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt64  |
	| 1         | "2018-11-13"                   | false  | JsonUInt64  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt64  |
	| 1         | "2018-11-13"                   | false  | JsonUInt64  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt64  |
	| 1         | "www.example.com"              | false  | JsonUInt64  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt64  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt64  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt64  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt64  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt64  |
	| 1         | "Hello"                        | false  | JsonUInt32  |
	| 1         | "Goodbye"                      | false  | JsonUInt32  |
	| 1         | 1                              | true   | JsonUInt32  |
	| 1         | 1.0                            | true   | JsonUInt32  |
	| 1         | 1.1                            | false  | JsonUInt32  |
	| 1         | [1,2,3]                        | false  | JsonUInt32  |
	| 1         | { "first": "1" }               | false  | JsonUInt32  |
	| 1         | true                           | false  | JsonUInt32  |
	| 1         | false                          | false  | JsonUInt32  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt32  |
	| 1         | "2018-11-13"                   | false  | JsonUInt32  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt32  |
	| 1         | "2018-11-13"                   | false  | JsonUInt32  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt32  |
	| 1         | "www.example.com"              | false  | JsonUInt32  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt32  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt32  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt32  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt32  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt32  |
	| 1         | "Hello"                        | false  | JsonUInt16  |
	| 1         | "Goodbye"                      | false  | JsonUInt16  |
	| 1         | 1                              | true   | JsonUInt16  |
	| 1         | 1.0                            | true   | JsonUInt16  |
	| 1         | 1.1                            | false  | JsonUInt16  |
	| 1         | [1,2,3]                        | false  | JsonUInt16  |
	| 1         | { "first": "1" }               | false  | JsonUInt16  |
	| 1         | true                           | false  | JsonUInt16  |
	| 1         | false                          | false  | JsonUInt16  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt16  |
	| 1         | "2018-11-13"                   | false  | JsonUInt16  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt16  |
	| 1         | "2018-11-13"                   | false  | JsonUInt16  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt16  |
	| 1         | "www.example.com"              | false  | JsonUInt16  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt16  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt16  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt16  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt16  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt16  |
	| 1         | "Hello"                        | false  | JsonByte    |
	| 1         | "Goodbye"                      | false  | JsonByte    |
	| 1         | 1                              | true   | JsonByte    |
	| 1         | 1.0                            | true   | JsonByte    |
	| 1         | 1.1                            | false  | JsonByte    |
	| 1         | [1,2,3]                        | false  | JsonByte    |
	| 1         | { "first": "1" }               | false  | JsonByte    |
	| 1         | true                           | false  | JsonByte    |
	| 1         | false                          | false  | JsonByte    |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonByte    |
	| 1         | "2018-11-13"                   | false  | JsonByte    |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonByte    |
	| 1         | "2018-11-13"                   | false  | JsonByte    |
	| 1         | "hello@endjin.com"             | false  | JsonByte    |
	| 1         | "www.example.com"              | false  | JsonByte    |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonByte    |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonByte    |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonByte    |
	| 1         | "192.168.0.1"                  | false  | JsonByte    |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonByte    |

Scenario Outline: Equals for integer dotnet backed value as an IJsonValue
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result | TargetType  |
	| 1         | "Hello"                        | false  | JsonInteger |
	| 1         | "Goodbye"                      | false  | JsonInteger |
	| 1         | 1                              | true   | JsonInteger |
	| 1         | 1.0                            | true   | JsonInteger |
	| 1         | 1.1                            | false  | JsonInteger |
	| 1         | [1,2,3]                        | false  | JsonInteger |
	| 1         | { "first": "1" }               | false  | JsonInteger |
	| 1         | true                           | false  | JsonInteger |
	| 1         | false                          | false  | JsonInteger |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInteger |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInteger |
	| 1         | "2018-11-13"                   | false  | JsonInteger |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInteger |
	| 1         | "hello@endjin.com"             | false  | JsonInteger |
	| 1         | "www.example.com"              | false  | JsonInteger |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInteger |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInteger |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInteger |
	| 1         | "192.168.0.1"                  | false  | JsonInteger |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInteger |
	| 1         | "Hello"                        | false  | JsonInt64   |
	| 1         | "Goodbye"                      | false  | JsonInt64   |
	| 1         | 1                              | true   | JsonInt64   |
	| 1         | 1.0                            | true   | JsonInt64   |
	| 1         | 1.1                            | false  | JsonInt64   |
	| 1         | [1,2,3]                        | false  | JsonInt64   |
	| 1         | { "first": "1" }               | false  | JsonInt64   |
	| 1         | true                           | false  | JsonInt64   |
	| 1         | false                          | false  | JsonInt64   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt64   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt64   |
	| 1         | "2018-11-13"                   | false  | JsonInt64   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt64   |
	| 1         | "hello@endjin.com"             | false  | JsonInt64   |
	| 1         | "www.example.com"              | false  | JsonInt64   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt64   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt64   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt64   |
	| 1         | "192.168.0.1"                  | false  | JsonInt64   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt64   |
	| 1         | "Hello"                        | false  | JsonInt32   |
	| 1         | "Goodbye"                      | false  | JsonInt32   |
	| 1         | 1                              | true   | JsonInt32   |
	| 1         | 1.0                            | true   | JsonInt32   |
	| 1         | 1.1                            | false  | JsonInt32   |
	| 1         | [1,2,3]                        | false  | JsonInt32   |
	| 1         | { "first": "1" }               | false  | JsonInt32   |
	| 1         | true                           | false  | JsonInt32   |
	| 1         | false                          | false  | JsonInt32   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt32   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt32   |
	| 1         | "2018-11-13"                   | false  | JsonInt32   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt32   |
	| 1         | "hello@endjin.com"             | false  | JsonInt32   |
	| 1         | "www.example.com"              | false  | JsonInt32   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt32   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt32   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt32   |
	| 1         | "192.168.0.1"                  | false  | JsonInt32   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt32   |
	| 1         | "Hello"                        | false  | JsonInt16   |
	| 1         | "Goodbye"                      | false  | JsonInt16   |
	| 1         | 1                              | true   | JsonInt16   |
	| 1         | 1.0                            | true   | JsonInt16   |
	| 1         | 1.1                            | false  | JsonInt16   |
	| 1         | [1,2,3]                        | false  | JsonInt16   |
	| 1         | { "first": "1" }               | false  | JsonInt16   |
	| 1         | true                           | false  | JsonInt16   |
	| 1         | false                          | false  | JsonInt16   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt16   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt16   |
	| 1         | "2018-11-13"                   | false  | JsonInt16   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt16   |
	| 1         | "hello@endjin.com"             | false  | JsonInt16   |
	| 1         | "www.example.com"              | false  | JsonInt16   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt16   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt16   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt16   |
	| 1         | "192.168.0.1"                  | false  | JsonInt16   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt16   |
	| 1         | "Hello"                        | false  | JsonSByte   |
	| 1         | "Goodbye"                      | false  | JsonSByte   |
	| 1         | 1                              | true   | JsonSByte   |
	| 1         | 1.0                            | true   | JsonSByte   |
	| 1         | 1.1                            | false  | JsonSByte   |
	| 1         | [1,2,3]                        | false  | JsonSByte   |
	| 1         | { "first": "1" }               | false  | JsonSByte   |
	| 1         | true                           | false  | JsonSByte   |
	| 1         | false                          | false  | JsonSByte   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonSByte   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonSByte   |
	| 1         | "2018-11-13"                   | false  | JsonSByte   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonSByte   |
	| 1         | "hello@endjin.com"             | false  | JsonSByte   |
	| 1         | "www.example.com"              | false  | JsonSByte   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonSByte   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonSByte   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonSByte   |
	| 1         | "192.168.0.1"                  | false  | JsonSByte   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonSByte   |
	| 1         | "Hello"                        | false  | JsonUInt64  |
	| 1         | "Goodbye"                      | false  | JsonUInt64  |
	| 1         | 1                              | true   | JsonUInt64  |
	| 1         | 1.0                            | true   | JsonUInt64  |
	| 1         | 1.1                            | false  | JsonUInt64  |
	| 1         | [1,2,3]                        | false  | JsonUInt64  |
	| 1         | { "first": "1" }               | false  | JsonUInt64  |
	| 1         | true                           | false  | JsonUInt64  |
	| 1         | false                          | false  | JsonUInt64  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt64  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt64  |
	| 1         | "2018-11-13"                   | false  | JsonUInt64  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt64  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt64  |
	| 1         | "www.example.com"              | false  | JsonUInt64  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt64  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt64  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt64  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt64  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt64  |
	| 1         | "Hello"                        | false  | JsonUInt32  |
	| 1         | "Goodbye"                      | false  | JsonUInt32  |
	| 1         | 1                              | true   | JsonUInt32  |
	| 1         | 1.0                            | true   | JsonUInt32  |
	| 1         | 1.1                            | false  | JsonUInt32  |
	| 1         | [1,2,3]                        | false  | JsonUInt32  |
	| 1         | { "first": "1" }               | false  | JsonUInt32  |
	| 1         | true                           | false  | JsonUInt32  |
	| 1         | false                          | false  | JsonUInt32  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt32  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt32  |
	| 1         | "2018-11-13"                   | false  | JsonUInt32  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt32  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt32  |
	| 1         | "www.example.com"              | false  | JsonUInt32  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt32  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt32  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt32  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt32  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt32  |
	| 1         | "Hello"                        | false  | JsonUInt16  |
	| 1         | "Goodbye"                      | false  | JsonUInt16  |
	| 1         | 1                              | true   | JsonUInt16  |
	| 1         | 1.0                            | true   | JsonUInt16  |
	| 1         | 1.1                            | false  | JsonUInt16  |
	| 1         | [1,2,3]                        | false  | JsonUInt16  |
	| 1         | { "first": "1" }               | false  | JsonUInt16  |
	| 1         | true                           | false  | JsonUInt16  |
	| 1         | false                          | false  | JsonUInt16  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt16  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt16  |
	| 1         | "2018-11-13"                   | false  | JsonUInt16  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt16  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt16  |
	| 1         | "www.example.com"              | false  | JsonUInt16  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt16  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt16  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt16  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt16  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt16  |
	| 1         | "Hello"                        | false  | JsonByte    |
	| 1         | "Goodbye"                      | false  | JsonByte    |
	| 1         | 1                              | true   | JsonByte    |
	| 1         | 1.0                            | true   | JsonByte    |
	| 1         | 1.1                            | false  | JsonByte    |
	| 1         | [1,2,3]                        | false  | JsonByte    |
	| 1         | { "first": "1" }               | false  | JsonByte    |
	| 1         | true                           | false  | JsonByte    |
	| 1         | false                          | false  | JsonByte    |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonByte    |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonByte    |
	| 1         | "2018-11-13"                   | false  | JsonByte    |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonByte    |
	| 1         | "hello@endjin.com"             | false  | JsonByte    |
	| 1         | "www.example.com"              | false  | JsonByte    |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonByte    |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonByte    |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonByte    |
	| 1         | "192.168.0.1"                  | false  | JsonByte    |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonByte    |

Scenario Outline: Equals for integer json element backed value as an object
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result | TargetType  |
	| 1         | "Hello"                        | false  | JsonInteger |
	| 1         | "Goodbye"                      | false  | JsonInteger |
	| 1         | 1                              | true   | JsonInteger |
	| 1         | 1.0                            | true   | JsonInteger |
	| 1         | 1.1                            | false  | JsonInteger |
	| 1         | [1,2,3]                        | false  | JsonInteger |
	| 1         | { "first": "1" }               | false  | JsonInteger |
	| 1         | true                           | false  | JsonInteger |
	| 1         | false                          | false  | JsonInteger |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInteger |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInteger |
	| 1         | "2018-11-13"                   | false  | JsonInteger |
	| 1         | "hello@endjin.com"             | false  | JsonInteger |
	| 1         | "www.example.com"              | false  | JsonInteger |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInteger |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInteger |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInteger |
	| 1         | "192.168.0.1"                  | false  | JsonInteger |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInteger |
	| 1         | <new object()>                 | false  | JsonInteger |
	| 1         | null                           | false  | JsonInteger |

	| 1         | "Hello"                        | false  | JsonInt64   |
	| 1         | "Goodbye"                      | false  | JsonInt64   |
	| 1         | 1                              | true   | JsonInt64   |
	| 1         | 1.0                            | true   | JsonInt64   |
	| 1         | 1.1                            | false  | JsonInt64   |
	| 1         | [1,2,3]                        | false  | JsonInt64   |
	| 1         | { "first": "1" }               | false  | JsonInt64   |
	| 1         | true                           | false  | JsonInt64   |
	| 1         | false                          | false  | JsonInt64   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt64   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt64   |
	| 1         | "2018-11-13"                   | false  | JsonInt64   |
	| 1         | "hello@endjin.com"             | false  | JsonInt64   |
	| 1         | "www.example.com"              | false  | JsonInt64   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt64   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt64   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt64   |
	| 1         | "192.168.0.1"                  | false  | JsonInt64   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt64   |
	| 1         | <new object()>                 | false  | JsonInt64   |
	| 1         | null                           | false  | JsonInt64   |

	| 1         | "Hello"                        | false  | JsonInt32   |
	| 1         | "Goodbye"                      | false  | JsonInt32   |
	| 1         | 1                              | true   | JsonInt32   |
	| 1         | 1.0                            | true   | JsonInt32   |
	| 1         | 1.1                            | false  | JsonInt32   |
	| 1         | [1,2,3]                        | false  | JsonInt32   |
	| 1         | { "first": "1" }               | false  | JsonInt32   |
	| 1         | true                           | false  | JsonInt32   |
	| 1         | false                          | false  | JsonInt32   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt32   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt32   |
	| 1         | "2018-11-13"                   | false  | JsonInt32   |
	| 1         | "hello@endjin.com"             | false  | JsonInt32   |
	| 1         | "www.example.com"              | false  | JsonInt32   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt32   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt32   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt32   |
	| 1         | "192.168.0.1"                  | false  | JsonInt32   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt32   |
	| 1         | <new object()>                 | false  | JsonInt32   |
	| 1         | null                           | false  | JsonInt32   |

	| 1         | "Hello"                        | false  | JsonInt16   |
	| 1         | "Goodbye"                      | false  | JsonInt16   |
	| 1         | 1                              | true   | JsonInt16   |
	| 1         | 1.0                            | true   | JsonInt16   |
	| 1         | 1.1                            | false  | JsonInt16   |
	| 1         | [1,2,3]                        | false  | JsonInt16   |
	| 1         | { "first": "1" }               | false  | JsonInt16   |
	| 1         | true                           | false  | JsonInt16   |
	| 1         | false                          | false  | JsonInt16   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt16   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt16   |
	| 1         | "2018-11-13"                   | false  | JsonInt16   |
	| 1         | "hello@endjin.com"             | false  | JsonInt16   |
	| 1         | "www.example.com"              | false  | JsonInt16   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt16   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt16   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt16   |
	| 1         | "192.168.0.1"                  | false  | JsonInt16   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt16   |
	| 1         | <new object()>                 | false  | JsonInt16   |
	| 1         | null                           | false  | JsonInt16   |

	| 1         | "Hello"                        | false  | JsonSByte   |
	| 1         | "Goodbye"                      | false  | JsonSByte   |
	| 1         | 1                              | true   | JsonSByte   |
	| 1         | 1.0                            | true   | JsonSByte   |
	| 1         | 1.1                            | false  | JsonSByte   |
	| 1         | [1,2,3]                        | false  | JsonSByte   |
	| 1         | { "first": "1" }               | false  | JsonSByte   |
	| 1         | true                           | false  | JsonSByte   |
	| 1         | false                          | false  | JsonSByte   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonSByte   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonSByte   |
	| 1         | "2018-11-13"                   | false  | JsonSByte   |
	| 1         | "hello@endjin.com"             | false  | JsonSByte   |
	| 1         | "www.example.com"              | false  | JsonSByte   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonSByte   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonSByte   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonSByte   |
	| 1         | "192.168.0.1"                  | false  | JsonSByte   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonSByte   |
	| 1         | <new object()>                 | false  | JsonSByte   |
	| 1         | null                           | false  | JsonSByte   |

	| 1         | "Hello"                        | false  | JsonUInt64  |
	| 1         | "Goodbye"                      | false  | JsonUInt64  |
	| 1         | 1                              | true   | JsonUInt64  |
	| 1         | 1.0                            | true   | JsonUInt64  |
	| 1         | 1.1                            | false  | JsonUInt64  |
	| 1         | [1,2,3]                        | false  | JsonUInt64  |
	| 1         | { "first": "1" }               | false  | JsonUInt64  |
	| 1         | true                           | false  | JsonUInt64  |
	| 1         | false                          | false  | JsonUInt64  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt64  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt64  |
	| 1         | "2018-11-13"                   | false  | JsonUInt64  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt64  |
	| 1         | "www.example.com"              | false  | JsonUInt64  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt64  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt64  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt64  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt64  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt64  |
	| 1         | <new object()>                 | false  | JsonUInt64  |
	| 1         | null                           | false  | JsonUInt64  |

	| 1         | "Hello"                        | false  | JsonUInt32  |
	| 1         | "Goodbye"                      | false  | JsonUInt32  |
	| 1         | 1                              | true   | JsonUInt32  |
	| 1         | 1.0                            | true   | JsonUInt32  |
	| 1         | 1.1                            | false  | JsonUInt32  |
	| 1         | [1,2,3]                        | false  | JsonUInt32  |
	| 1         | { "first": "1" }               | false  | JsonUInt32  |
	| 1         | true                           | false  | JsonUInt32  |
	| 1         | false                          | false  | JsonUInt32  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt32  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt32  |
	| 1         | "2018-11-13"                   | false  | JsonUInt32  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt32  |
	| 1         | "www.example.com"              | false  | JsonUInt32  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt32  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt32  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt32  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt32  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt32  |
	| 1         | <new object()>                 | false  | JsonUInt32  |
	| 1         | null                           | false  | JsonUInt32  |

	| 1         | "Hello"                        | false  | JsonUInt16  |
	| 1         | "Goodbye"                      | false  | JsonUInt16  |
	| 1         | 1                              | true   | JsonUInt16  |
	| 1         | 1.0                            | true   | JsonUInt16  |
	| 1         | 1.1                            | false  | JsonUInt16  |
	| 1         | [1,2,3]                        | false  | JsonUInt16  |
	| 1         | { "first": "1" }               | false  | JsonUInt16  |
	| 1         | true                           | false  | JsonUInt16  |
	| 1         | false                          | false  | JsonUInt16  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt16  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt16  |
	| 1         | "2018-11-13"                   | false  | JsonUInt16  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt16  |
	| 1         | "www.example.com"              | false  | JsonUInt16  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt16  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt16  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt16  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt16  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt16  |
	| 1         | <new object()>                 | false  | JsonUInt16  |
	| 1         | null                           | false  | JsonUInt16  |

	| 1         | "Hello"                        | false  | JsonByte    |
	| 1         | "Goodbye"                      | false  | JsonByte    |
	| 1         | 1                              | true   | JsonByte    |
	| 1         | 1.0                            | true   | JsonByte    |
	| 1         | 1.1                            | false  | JsonByte    |
	| 1         | [1,2,3]                        | false  | JsonByte    |
	| 1         | { "first": "1" }               | false  | JsonByte    |
	| 1         | true                           | false  | JsonByte    |
	| 1         | false                          | false  | JsonByte    |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonByte    |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonByte    |
	| 1         | "2018-11-13"                   | false  | JsonByte    |
	| 1         | "hello@endjin.com"             | false  | JsonByte    |
	| 1         | "www.example.com"              | false  | JsonByte    |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonByte    |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonByte    |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonByte    |
	| 1         | "192.168.0.1"                  | false  | JsonByte    |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonByte    |
	| 1         | <new object()>                 | false  | JsonByte    |
	| 1         | null                           | false  | JsonByte    |

Scenario Outline: Equals for integer dotnet backed value as an object
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result | TargetType  |
	| 1         | "Hello"                        | false  | JsonInteger |
	| 1         | "Goodbye"                      | false  | JsonInteger |
	| 1         | 1                              | true   | JsonInteger |
	| 1         | 1.0                            | true   | JsonInteger |
	| 1         | 1.1                            | false  | JsonInteger |
	| 1         | [1,2,3]                        | false  | JsonInteger |
	| 1         | { "first": "1" }               | false  | JsonInteger |
	| 1         | true                           | false  | JsonInteger |
	| 1         | false                          | false  | JsonInteger |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInteger |
	| 1         | "2018-11-13"                   | false  | JsonInteger |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInteger |
	| 1         | "hello@endjin.com"             | false  | JsonInteger |
	| 1         | "www.example.com"              | false  | JsonInteger |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInteger |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInteger |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInteger |
	| 1         | "192.168.0.1"                  | false  | JsonInteger |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInteger |
	| 1         | <new object()>                 | false  | JsonInteger |
	| 1         | null                           | false  | JsonInteger |
	| 1         | <null>                         | false  | JsonInteger |
	| 1         | <undefined>                    | false  | JsonInteger |
	| null      | null                           | true   | JsonInteger |
	| null      | <null>                         | true   | JsonInteger |
	| null      | <undefined>                    | false  | JsonInteger |

	| 1         | "Hello"                        | false  | JsonInt64   |
	| 1         | "Goodbye"                      | false  | JsonInt64   |
	| 1         | 1                              | true   | JsonInt64   |
	| 1         | 1.0                            | true   | JsonInt64   |
	| 1         | 1.1                            | false  | JsonInt64   |
	| 1         | [1,2,3]                        | false  | JsonInt64   |
	| 1         | { "first": "1" }               | false  | JsonInt64   |
	| 1         | true                           | false  | JsonInt64   |
	| 1         | false                          | false  | JsonInt64   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt64   |
	| 1         | "2018-11-13"                   | false  | JsonInt64   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt64   |
	| 1         | "hello@endjin.com"             | false  | JsonInt64   |
	| 1         | "www.example.com"              | false  | JsonInt64   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt64   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt64   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt64   |
	| 1         | "192.168.0.1"                  | false  | JsonInt64   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt64   |
	| 1         | <new object()>                 | false  | JsonInt64   |
	| 1         | null                           | false  | JsonInt64   |
	| 1         | <null>                         | false  | JsonInt64   |
	| 1         | <undefined>                    | false  | JsonInt64   |
	| null      | null                           | true   | JsonInt64   |
	| null      | <null>                         | true   | JsonInt64   |
	| null      | <undefined>                    | false  | JsonInt64   |

	| 1         | "Hello"                        | false  | JsonInt32   |
	| 1         | "Goodbye"                      | false  | JsonInt32   |
	| 1         | 1                              | true   | JsonInt32   |
	| 1         | 1.0                            | true   | JsonInt32   |
	| 1         | 1.1                            | false  | JsonInt32   |
	| 1         | [1,2,3]                        | false  | JsonInt32   |
	| 1         | { "first": "1" }               | false  | JsonInt32   |
	| 1         | true                           | false  | JsonInt32   |
	| 1         | false                          | false  | JsonInt32   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt32   |
	| 1         | "2018-11-13"                   | false  | JsonInt32   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt32   |
	| 1         | "hello@endjin.com"             | false  | JsonInt32   |
	| 1         | "www.example.com"              | false  | JsonInt32   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt32   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt32   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt32   |
	| 1         | "192.168.0.1"                  | false  | JsonInt32   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt32   |
	| 1         | <new object()>                 | false  | JsonInt32   |
	| 1         | null                           | false  | JsonInt32   |
	| 1         | <null>                         | false  | JsonInt32   |
	| 1         | <undefined>                    | false  | JsonInt32   |
	| null      | null                           | true   | JsonInt32   |
	| null      | <null>                         | true   | JsonInt32   |
	| null      | <undefined>                    | false  | JsonInt32   |

	| 1         | "Hello"                        | false  | JsonInt16   |
	| 1         | "Goodbye"                      | false  | JsonInt16   |
	| 1         | 1                              | true   | JsonInt16   |
	| 1         | 1.0                            | true   | JsonInt16   |
	| 1         | 1.1                            | false  | JsonInt16   |
	| 1         | [1,2,3]                        | false  | JsonInt16   |
	| 1         | { "first": "1" }               | false  | JsonInt16   |
	| 1         | true                           | false  | JsonInt16   |
	| 1         | false                          | false  | JsonInt16   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonInt16   |
	| 1         | "2018-11-13"                   | false  | JsonInt16   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonInt16   |
	| 1         | "hello@endjin.com"             | false  | JsonInt16   |
	| 1         | "www.example.com"              | false  | JsonInt16   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonInt16   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonInt16   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonInt16   |
	| 1         | "192.168.0.1"                  | false  | JsonInt16   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonInt16   |
	| 1         | <new object()>                 | false  | JsonInt16   |
	| 1         | null                           | false  | JsonInt16   |
	| 1         | <null>                         | false  | JsonInt16   |
	| 1         | <undefined>                    | false  | JsonInt16   |
	| null      | null                           | true   | JsonInt16   |
	| null      | <null>                         | true   | JsonInt16   |
	| null      | <undefined>                    | false  | JsonInt16   |

	| 1         | "Hello"                        | false  | JsonSByte   |
	| 1         | "Goodbye"                      | false  | JsonSByte   |
	| 1         | 1                              | true   | JsonSByte   |
	| 1         | 1.0                            | true   | JsonSByte   |
	| 1         | 1.1                            | false  | JsonSByte   |
	| 1         | [1,2,3]                        | false  | JsonSByte   |
	| 1         | { "first": "1" }               | false  | JsonSByte   |
	| 1         | true                           | false  | JsonSByte   |
	| 1         | false                          | false  | JsonSByte   |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonSByte   |
	| 1         | "2018-11-13"                   | false  | JsonSByte   |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonSByte   |
	| 1         | "hello@endjin.com"             | false  | JsonSByte   |
	| 1         | "www.example.com"              | false  | JsonSByte   |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonSByte   |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonSByte   |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonSByte   |
	| 1         | "192.168.0.1"                  | false  | JsonSByte   |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonSByte   |
	| 1         | <new object()>                 | false  | JsonSByte   |
	| 1         | null                           | false  | JsonSByte   |
	| 1         | <null>                         | false  | JsonSByte   |
	| 1         | <undefined>                    | false  | JsonSByte   |
	| null      | null                           | true   | JsonSByte   |
	| null      | <null>                         | true   | JsonSByte   |
	| null      | <undefined>                    | false  | JsonSByte   |

	| 1         | "Hello"                        | false  | JsonUInt64  |
	| 1         | "Goodbye"                      | false  | JsonUInt64  |
	| 1         | 1                              | true   | JsonUInt64  |
	| 1         | 1.0                            | true   | JsonUInt64  |
	| 1         | 1.1                            | false  | JsonUInt64  |
	| 1         | [1,2,3]                        | false  | JsonUInt64  |
	| 1         | { "first": "1" }               | false  | JsonUInt64  |
	| 1         | true                           | false  | JsonUInt64  |
	| 1         | false                          | false  | JsonUInt64  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt64  |
	| 1         | "2018-11-13"                   | false  | JsonUInt64  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt64  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt64  |
	| 1         | "www.example.com"              | false  | JsonUInt64  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt64  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt64  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt64  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt64  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt64  |
	| 1         | <new object()>                 | false  | JsonUInt64  |
	| 1         | null                           | false  | JsonUInt64  |
	| 1         | <null>                         | false  | JsonUInt64  |
	| 1         | <undefined>                    | false  | JsonUInt64  |
	| null      | null                           | true   | JsonUInt64  |
	| null      | <null>                         | true   | JsonUInt64  |
	| null      | <undefined>                    | false  | JsonUInt64  |

	| 1         | "Hello"                        | false  | JsonUInt32  |
	| 1         | "Goodbye"                      | false  | JsonUInt32  |
	| 1         | 1                              | true   | JsonUInt32  |
	| 1         | 1.0                            | true   | JsonUInt32  |
	| 1         | 1.1                            | false  | JsonUInt32  |
	| 1         | [1,2,3]                        | false  | JsonUInt32  |
	| 1         | { "first": "1" }               | false  | JsonUInt32  |
	| 1         | true                           | false  | JsonUInt32  |
	| 1         | false                          | false  | JsonUInt32  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt32  |
	| 1         | "2018-11-13"                   | false  | JsonUInt32  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt32  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt32  |
	| 1         | "www.example.com"              | false  | JsonUInt32  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt32  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt32  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt32  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt32  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt32  |
	| 1         | <new object()>                 | false  | JsonUInt32  |
	| 1         | null                           | false  | JsonUInt32  |
	| 1         | <null>                         | false  | JsonUInt32  |
	| 1         | <undefined>                    | false  | JsonUInt32  |
	| null      | null                           | true   | JsonUInt32  |
	| null      | <null>                         | true   | JsonUInt32  |
	| null      | <undefined>                    | false  | JsonUInt32  |

	| 1         | "Hello"                        | false  | JsonUInt16  |
	| 1         | "Goodbye"                      | false  | JsonUInt16  |
	| 1         | 1                              | true   | JsonUInt16  |
	| 1         | 1.0                            | true   | JsonUInt16  |
	| 1         | 1.1                            | false  | JsonUInt16  |
	| 1         | [1,2,3]                        | false  | JsonUInt16  |
	| 1         | { "first": "1" }               | false  | JsonUInt16  |
	| 1         | true                           | false  | JsonUInt16  |
	| 1         | false                          | false  | JsonUInt16  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonUInt16  |
	| 1         | "2018-11-13"                   | false  | JsonUInt16  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonUInt16  |
	| 1         | "hello@endjin.com"             | false  | JsonUInt16  |
	| 1         | "www.example.com"              | false  | JsonUInt16  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonUInt16  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonUInt16  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonUInt16  |
	| 1         | "192.168.0.1"                  | false  | JsonUInt16  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonUInt16  |
	| 1         | <new object()>                 | false  | JsonUInt16  |
	| 1         | null                           | false  | JsonUInt16  |
	| 1         | <null>                         | false  | JsonUInt16  |
	| 1         | <undefined>                    | false  | JsonUInt16  |
	| null      | null                           | true   | JsonUInt16  |
	| null      | <null>                         | true   | JsonUInt16  |
	| null      | <undefined>                    | false  | JsonUInt16  |

	| 1         | "Hello"                        | false  | JsonByte    |
	| 1         | "Goodbye"                      | false  | JsonByte    |
	| 1         | 1                              | true   | JsonByte    |
	| 1         | 1.0                            | true   | JsonByte    |
	| 1         | 1.1                            | false  | JsonByte    |
	| 1         | [1,2,3]                        | false  | JsonByte    |
	| 1         | { "first": "1" }               | false  | JsonByte    |
	| 1         | true                           | false  | JsonByte    |
	| 1         | false                          | false  | JsonByte    |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  | JsonByte    |
	| 1         | "2018-11-13"                   | false  | JsonByte    |
	| 1         | "P3Y6M4DT12H30M5S"             | false  | JsonByte    |
	| 1         | "hello@endjin.com"             | false  | JsonByte    |
	| 1         | "www.example.com"              | false  | JsonByte    |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  | JsonByte    |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonByte    |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  | JsonByte    |
	| 1         | "192.168.0.1"                  | false  | JsonByte    |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonByte    |
	| 1         | <new object()>                 | false  | JsonByte    |
	| 1         | null                           | false  | JsonByte    |
	| 1         | <null>                         | false  | JsonByte    |
	| 1         | <undefined>                    | false  | JsonByte    |
	| null      | null                           | true   | JsonByte    |
	| null      | <null>                         | true   | JsonByte    |
	| null      | <undefined>                    | false  | JsonByte    |