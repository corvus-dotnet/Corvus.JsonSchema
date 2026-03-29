Feature: MutatingVisitor

As a developer I want to efficiently walk an immutable JSON tree, producing a mutated version.

Scenario Outline: Removing elements from the document
	Given the json document <document>
	When I visit with the <visitor>
	Then I expect the result <toBeModifiedOrNot>
	And the resulting document to be <expected>

Examples:
	| document                                                                | visitor             | toBeModifiedOrNot  | expected                           |
	| ["hello","there","everyone"]                                            | RemoveThereString   | to be modified     | ["hello","everyone"]               |
	| ["there", "hello","everyone"]                                           | RemoveThereString   | to be modified     | ["hello","everyone"]               |
	| ["hello", "everyone", "there"]                                          | RemoveThereString   | to be modified     | ["hello","everyone"]               |
	| ["there", "hello", "there", "everyone", "there"]                        | RemoveThereString   | to be modified     | ["hello","everyone"]               |
	| ["there", "hello", "there", ["Fi", "Fi", "there"], "everyone", "there"] | RemoveThereString   | to be modified     | ["hello", ["Fi", "Fi"],"everyone"] |
	| ["there"]                                                               | RemoveThereString   | to be modified     | []                                 |
	| ["hello","you","all"]                                                   | RemoveThereString   | not to be modified | ["hello","you","all"]              |
	| "there"                                                                 | RemoveThereString   | to be modified     | <undefined>                        |
	| {"hello":1,"there":2,"everyone":3}                                      | RemoveThereProperty | to be modified     | {"hello":1,"everyone":3}           |
	| {"there":2,"hello":1,"everyone":3}                                      | RemoveThereProperty | to be modified     | {"hello":1,"everyone":3}           |
	| {"hello":1,"everyone":3,"there":2}                                      | RemoveThereProperty | to be modified     | {"hello":1,"everyone":3}           |
	| {"there":2}                                                             | RemoveThereProperty | to be modified     | {}                                 |
	| {"hello":1,"you":2,"all":3}                                             | RemoveThereProperty | not to be modified | {"hello":1,"you":2,"all":3}        |