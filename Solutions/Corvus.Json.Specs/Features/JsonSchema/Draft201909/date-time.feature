@draft2019-09

Feature: date-time draft2019-09
    In order to use json-schema
    As a developer
    I want to support date-time in draft2019-09

Scenario Outline: validation of date-time strings
/* Schema: 
{"format": "date-time"}
*/
    Given the input JSON file "date-time.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid date-time string                                                         |
        | #/000/tests/001/data | true  | a valid date-time string without second fraction                                 |
        | #/000/tests/002/data | true  | a valid date-time string with plus offset                                        |
        | #/000/tests/003/data | true  | a valid date-time string with minus offset                                       |
        | #/000/tests/004/data | false | a invalid day in date-time string                                                |
        | #/000/tests/005/data | false | an invalid offset in date-time string                                            |
        | #/000/tests/006/data | false | an invalid date-time string                                                      |
        | #/000/tests/007/data | true  | case-insensitive T and Z                                                         |
        | #/000/tests/008/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        | #/000/tests/009/data | false | invalid non-padded month dates                                                   |
        | #/000/tests/010/data | false | invalid non-padded day dates                                                     |
