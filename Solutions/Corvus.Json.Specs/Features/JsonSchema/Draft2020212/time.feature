@draft2020-12

Feature: time draft2020-12
    In order to use json-schema
    As a developer
    I want to support time in draft2020-12

Scenario Outline: validation of time strings
/* Schema: 
{"format": "time"}
*/
    Given the input JSON file "time.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid time string                                                              |
        | #/000/tests/001/data | false | an invalid time string                                                           |
        | #/000/tests/002/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
