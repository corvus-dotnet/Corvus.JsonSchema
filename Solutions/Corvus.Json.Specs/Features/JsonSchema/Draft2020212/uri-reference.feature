@draft2020-12

Feature: uri-reference draft2020-12
    In order to use json-schema
    As a developer
    I want to support uri-reference in draft2020-12

Scenario Outline: validation of URI References
/* Schema: 
{ "format": "uri-reference" }
*/
    Given the input JSON file "optional\format\uri-reference.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        | #/000/tests/006/data | true  | a valid URI                                                                      |
        | #/000/tests/007/data | true  | a valid protocol-relative URI Reference                                          |
        | #/000/tests/008/data | true  | a valid relative URI Reference                                                   |
        | #/000/tests/009/data | false | an invalid URI Reference                                                         |
        | #/000/tests/010/data | true  | a valid URI Reference                                                            |
        | #/000/tests/011/data | true  | a valid URI fragment                                                             |
        | #/000/tests/012/data | false | an invalid URI fragment                                                          |
