Feature: JsonCompareMethod
  In order to ensure the correctness of the numeric comparison function
  As a developer
  I want to test the static compare function with various scenarios

Scenario Outline: Compare two json backed values using the static Compare() method
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> with the <TargetType> <right>
	Then the comparison result should equal the int <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | -1     | JsonInteger |
	| 20   | 10    | 1      | JsonInteger |
	| 15   | 15    | 0      | JsonInteger |
	| 10   | 20    | -1     | JsonInt128  |
	| 20   | 10    | 1      | JsonInt128  |
	| 15   | 15    | 0      | JsonInt128  |
	| 10   | 20    | -1     | JsonInt64   |
	| 20   | 10    | 1      | JsonInt64   |
	| 15   | 15    | 0      | JsonInt64   |
	| 10   | 20    | -1     | JsonInt32   |
	| 20   | 10    | 1      | JsonInt32   |
	| 15   | 15    | 0      | JsonInt32   |
	| 10   | 20    | -1     | JsonInt16   |
	| 20   | 10    | 1      | JsonInt16   |
	| 15   | 15    | 0      | JsonInt16   |
	| 10   | 20    | -1     | JsonSByte   |
	| 20   | 10    | 1      | JsonSByte   |
	| 15   | 15    | 0      | JsonSByte   |
	| 10   | 20    | -1     | JsonUInt64  |
	| 20   | 10    | 1      | JsonUInt64  |
	| 15   | 15    | 0      | JsonUInt64  |
	| 10   | 20    | -1     | JsonUInt128 |
	| 20   | 10    | 1      | JsonUInt128 |
	| 15   | 15    | 0      | JsonUInt128 |
	| 10   | 20    | -1     | JsonUInt32  |
	| 20   | 10    | 1      | JsonUInt32  |
	| 15   | 15    | 0      | JsonUInt32  |
	| 10   | 20    | -1     | JsonUInt16  |
	| 20   | 10    | 1      | JsonUInt16  |
	| 15   | 15    | 0      | JsonUInt16  |
	| 10   | 20    | -1     | JsonByte    |
	| 20   | 10    | 1      | JsonByte    |
	| 15   | 15    | 0      | JsonByte    |
	| 10.1 | 20.1  | -1     | JsonNumber  |
	| 20.1 | 10.1  | 1      | JsonNumber  |
	| 15.1 | 15.1  | 0      | JsonNumber  |
	| 10.1 | 20.1  | -1     | JsonSingle  |
	| 20.1 | 10.1  | 1      | JsonSingle  |
	| 15.1 | 15.1  | 0      | JsonSingle  |
	| 10.1 | 20.1  | -1     | JsonHalf    |
	| 20.1 | 10.1  | 1      | JsonHalf    |
	| 15.1 | 15.1  | 0      | JsonHalf    |
	| 10.1 | 20.1  | -1     | JsonDecimal |
	| 20.1 | 10.1  | 1      | JsonDecimal |
	| 15.1 | 15.1  | 0      | JsonDecimal |
	| 10.1 | 20.1  | -1     | JsonDouble  |
	| 20.1 | 10.1  | 1      | JsonDouble  |
	| 15.1 | 15.1  | 0      | JsonDouble  |

Scenario Outline: Compare two dotnet backed values using the static Compare() method
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> with the <TargetType> <right>
	Then the comparison result should equal the int <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | -1     | JsonInteger |
	| 20   | 10    | 1      | JsonInteger |
	| 15   | 15    | 0      | JsonInteger |
	| 10   | 20    | -1     | JsonInt64   |
	| 20   | 10    | 1      | JsonInt64   |
	| 15   | 15    | 0      | JsonInt64   |
	| 10   | 20    | -1     | JsonInt128  |
	| 20   | 10    | 1      | JsonInt128  |
	| 15   | 15    | 0      | JsonInt128  |
	| 10   | 20    | -1     | JsonInt32   |
	| 20   | 10    | 1      | JsonInt32   |
	| 15   | 15    | 0      | JsonInt32   |
	| 10   | 20    | -1     | JsonInt16   |
	| 20   | 10    | 1      | JsonInt16   |
	| 15   | 15    | 0      | JsonInt16   |
	| 10   | 20    | -1     | JsonSByte   |
	| 20   | 10    | 1      | JsonSByte   |
	| 15   | 15    | 0      | JsonSByte   |
	| 10   | 20    | -1     | JsonUInt64  |
	| 20   | 10    | 1      | JsonUInt64  |
	| 15   | 15    | 0      | JsonUInt64  |
	| 10   | 20    | -1     | JsonUInt128 |
	| 20   | 10    | 1      | JsonUInt128 |
	| 15   | 15    | 0      | JsonUInt128 |
	| 10   | 20    | -1     | JsonUInt32  |
	| 20   | 10    | 1      | JsonUInt32  |
	| 15   | 15    | 0      | JsonUInt32  |
	| 10   | 20    | -1     | JsonUInt16  |
	| 20   | 10    | 1      | JsonUInt16  |
	| 15   | 15    | 0      | JsonUInt16  |
	| 10   | 20    | -1     | JsonByte    |
	| 20   | 10    | 1      | JsonByte    |
	| 15   | 15    | 0      | JsonByte    |
	| 10.1 | 20.1  | -1     | JsonNumber  |
	| 20.1 | 10.1  | 1      | JsonNumber  |
	| 15.1 | 15.1  | 0      | JsonNumber  |
	| 10.1 | 20.1  | -1     | JsonSingle  |
	| 20.1 | 10.1  | 1      | JsonSingle  |
	| 15.1 | 15.1  | 0      | JsonSingle  |
	| 10.1 | 20.1  | -1     | JsonDecimal |
	| 20.1 | 10.1  | 1      | JsonDecimal |
	| 15.1 | 15.1  | 0      | JsonDecimal |
	| 10.1 | 20.1  | -1     | JsonDouble  |
	| 20.1 | 10.1  | 1      | JsonDouble  |
	| 15.1 | 15.1  | 0      | JsonDouble  |