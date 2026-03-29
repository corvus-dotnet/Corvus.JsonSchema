@draft2020-12

Feature: exclusiveMaximum draft2020-12
    In order to use json-schema
    As a developer
    I want to support exclusiveMaximum in draft2020-12

Scenario Outline: exclusiveMaximum validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "exclusiveMaximum": 3.0
        }
*/
    Given the input JSON file "exclusiveMaximum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 2.2
        | #/000/tests/000/data | true  | below the exclusiveMaximum is valid                                              |
        # 3.0
        | #/000/tests/001/data | false | boundary point is invalid                                                        |
        # 3.5
        | #/000/tests/002/data | false | above the exclusiveMaximum is invalid                                            |
        # x
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |
