@draft2019-09

Feature: json-pointer draft2019-09
    In order to use json-schema
    As a developer
    I want to support json-pointer in draft2019-09

Scenario Outline: validation of JSON-pointers (JSON String Representation)
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "json-pointer"
        }
*/
    Given the input JSON file "optional/format/json-pointer.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        # /foo/bar~0/baz~1/%a
        | #/000/tests/006/data | true  | a valid JSON-pointer                                                             |
        # /foo/bar~
        | #/000/tests/007/data | false | not a valid JSON-pointer (~ not escaped)                                         |
        # /foo//bar
        | #/000/tests/008/data | true  | valid JSON-pointer with empty segment                                            |
        # /foo/bar/
        | #/000/tests/009/data | true  | valid JSON-pointer with the last empty segment                                   |
        # 
        | #/000/tests/010/data | true  | valid JSON-pointer as stated in RFC 6901 #1                                      |
        # /foo
        | #/000/tests/011/data | true  | valid JSON-pointer as stated in RFC 6901 #2                                      |
        # /foo/0
        | #/000/tests/012/data | true  | valid JSON-pointer as stated in RFC 6901 #3                                      |
        # /
        | #/000/tests/013/data | true  | valid JSON-pointer as stated in RFC 6901 #4                                      |
        # /a~1b
        | #/000/tests/014/data | true  | valid JSON-pointer as stated in RFC 6901 #5                                      |
        # /c%d
        | #/000/tests/015/data | true  | valid JSON-pointer as stated in RFC 6901 #6                                      |
        # /e^f
        | #/000/tests/016/data | true  | valid JSON-pointer as stated in RFC 6901 #7                                      |
        # /g|h
        | #/000/tests/017/data | true  | valid JSON-pointer as stated in RFC 6901 #8                                      |
        # /i\j
        | #/000/tests/018/data | true  | valid JSON-pointer as stated in RFC 6901 #9                                      |
        # /k"l
        | #/000/tests/019/data | true  | valid JSON-pointer as stated in RFC 6901 #10                                     |
        # / 
        | #/000/tests/020/data | true  | valid JSON-pointer as stated in RFC 6901 #11                                     |
        # /m~0n
        | #/000/tests/021/data | true  | valid JSON-pointer as stated in RFC 6901 #12                                     |
        # /foo/-
        | #/000/tests/022/data | true  | valid JSON-pointer used adding to the last array position                        |
        # /foo/-/bar
        | #/000/tests/023/data | true  | valid JSON-pointer (- used as object member name)                                |
        # /~1~0~0~1~1
        | #/000/tests/024/data | true  | valid JSON-pointer (multiple escaped characters)                                 |
        # /~1.1
        | #/000/tests/025/data | true  | valid JSON-pointer (escaped with fraction part) #1                               |
        # /~0.1
        | #/000/tests/026/data | true  | valid JSON-pointer (escaped with fraction part) #2                               |
        # #
        | #/000/tests/027/data | false | not a valid JSON-pointer (URI Fragment Identifier) #1                            |
        # #/
        | #/000/tests/028/data | false | not a valid JSON-pointer (URI Fragment Identifier) #2                            |
        # #a
        | #/000/tests/029/data | false | not a valid JSON-pointer (URI Fragment Identifier) #3                            |
        # /~0~
        | #/000/tests/030/data | false | not a valid JSON-pointer (some escaped, but not all) #1                          |
        # /~0/~
        | #/000/tests/031/data | false | not a valid JSON-pointer (some escaped, but not all) #2                          |
        # /~2
        | #/000/tests/032/data | false | not a valid JSON-pointer (wrong escape character) #1                             |
        # /~-1
        | #/000/tests/033/data | false | not a valid JSON-pointer (wrong escape character) #2                             |
        # /~~
        | #/000/tests/034/data | false | not a valid JSON-pointer (multiple characters not escaped)                       |
        # a
        | #/000/tests/035/data | false | not a valid JSON-pointer (isn't empty nor starts with /) #1                      |
        # 0
        | #/000/tests/036/data | false | not a valid JSON-pointer (isn't empty nor starts with /) #2                      |
        # a/a
        | #/000/tests/037/data | false | not a valid JSON-pointer (isn't empty nor starts with /) #3                      |
