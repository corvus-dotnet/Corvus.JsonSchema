@draft2019-09

Feature: uri-template draft2019-09
    In order to use json-schema
    As a developer
    I want to support uri-template in draft2019-09

Scenario Outline: format: uri-template
/* Schema: 
{ "format": "uri-template" }
*/
    Given the input JSON file "optional\format\uri-template.json"
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
        | #/000/tests/006/data | true  | a valid uri-template                                                             |
        | #/000/tests/007/data | false | an invalid uri-template                                                          |
        | #/000/tests/008/data | true  | a valid uri-template without variables                                           |
        | #/000/tests/009/data | true  | a valid relative uri-template                                                    |
