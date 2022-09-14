@draft2019-09

Feature: minItems draft2019-09
    In order to use json-schema
    As a developer
    I want to support minItems in draft2019-09

Scenario Outline: minItems validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "minItems": 1
        }
*/
    Given the input JSON file "minItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | longer is valid                                                                  |
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        | #/000/tests/002/data | false | too short is invalid                                                             |
        | #/000/tests/003/data | true  | ignores non-arrays                                                               |

Scenario Outline: minItems validation with a decimal
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "minItems": 1.0
        }
*/
    Given the input JSON file "minItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | longer is valid                                                                  |
        | #/001/tests/001/data | false | too short is invalid                                                             |
