@draft2019-09

Feature: maxLength draft2019-09
    In order to use json-schema
    As a developer
    I want to support maxLength in draft2019-09

Scenario Outline: maxLength validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "maxLength": 2
        }
*/
    Given the input JSON file "maxLength.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # f
        | #/000/tests/000/data | true  | shorter is valid                                                                 |
        # fo
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        # foo
        | #/000/tests/002/data | false | too long is invalid                                                              |
        # 100
        | #/000/tests/003/data | true  | ignores non-strings                                                              |
        # 💩💩
        | #/000/tests/004/data | true  | two graphemes is long enough                                                     |

Scenario Outline: maxLength validation with a decimal
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "maxLength": 2.0
        }
*/
    Given the input JSON file "maxLength.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # f
        | #/001/tests/000/data | true  | shorter is valid                                                                 |
        # foo
        | #/001/tests/001/data | false | too long is invalid                                                              |
