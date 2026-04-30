@draft2020-12

Feature: pattern draft2020-12
    In order to use json-schema
    As a developer
    I want to support pattern in draft2020-12

Scenario Outline: pattern validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "^a*$"
        }
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
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "a+"
        }
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

Scenario Outline: pattern with Unicode property escape requires unicode mode
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\p{Letter}+$"
        }
*/
    Given the input JSON file "pattern.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # Hello
        | #/002/tests/000/data | true  | ASCII letters match                                                              |
        # π
        | #/002/tests/001/data | true  | Non-ASCII letters match                                                          |
        # 123
        | #/002/tests/002/data | false | Digits do not match                                                              |
