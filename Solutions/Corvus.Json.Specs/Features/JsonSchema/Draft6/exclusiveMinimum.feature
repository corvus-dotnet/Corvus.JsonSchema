@draft6

Feature: exclusiveMinimum draft6
    In order to use json-schema
    As a developer
    I want to support exclusiveMinimum in draft6

Scenario Outline: exclusiveMinimum validation
/* Schema: 
{
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
        | #/000/tests/000/data | true  | above the exclusiveMinimum is valid                                              |
        | #/000/tests/001/data | false | boundary point is invalid                                                        |
        | #/000/tests/002/data | false | below the exclusiveMinimum is invalid                                            |
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |
