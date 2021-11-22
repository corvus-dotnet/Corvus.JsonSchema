@draft2020-12

Feature: date draft2020-12
    In order to use json-schema
    As a developer
    I want to support date in draft2020-12

Scenario Outline: validation of date strings
/* Schema: 
{"format": "date"}
*/
    Given the input JSON file "date.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid date string                                                              |
        | #/000/tests/001/data | false | an invalid date-time string                                                      |
        | #/000/tests/002/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        | #/000/tests/003/data | false | invalidates non-padded month dates                                               |
        | #/000/tests/004/data | false | invalidates non-padded day dates                                                 |
