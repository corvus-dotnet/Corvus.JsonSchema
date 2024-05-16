@draft4

Feature: pattern draft4
    In order to use json-schema
    As a developer
    I want to support pattern in draft4

Scenario Outline: pattern validation
/* Schema: 
{"pattern": "^a*$"}
*/
    Given the input JSON file "pattern.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # aaa
        | #/000/tests/000/data | true  | a matching pattern is valid                                                      |
        # abc
        | #/000/tests/001/data | false | a non-matching pattern is invalid                                                |
        # True
        | #/000/tests/002/data | true  | ignores booleans                                                                 |
        # 123
        | #/000/tests/003/data | true  | ignores integers                                                                 |
        # 1.0
        | #/000/tests/004/data | true  | ignores floats                                                                   |
        # {}
        | #/000/tests/005/data | true  | ignores objects                                                                  |
        # []
        | #/000/tests/006/data | true  | ignores arrays                                                                   |
        # 
        | #/000/tests/007/data | true  | ignores null                                                                     |

Scenario Outline: pattern is not anchored
/* Schema: 
{"pattern": "a+"}
*/
    Given the input JSON file "pattern.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # xxaayy
        | #/001/tests/000/data | true  | matches a substring                                                              |
