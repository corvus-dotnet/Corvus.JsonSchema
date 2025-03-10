@draft2019-09

Feature: exclusiveMinimum draft2019-09
    In order to use json-schema
    As a developer
    I want to support exclusiveMinimum in draft2019-09

Scenario Outline: exclusiveMinimum validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "exclusiveMinimum": 1.1
        }
*/
    Given the input JSON file "exclusiveMinimum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1.2
        | #/000/tests/000/data | true  | above the exclusiveMinimum is valid                                              |
        # 1.1
        | #/000/tests/001/data | false | boundary point is invalid                                                        |
        # 0.6
        | #/000/tests/002/data | false | below the exclusiveMinimum is invalid                                            |
        # x
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |
