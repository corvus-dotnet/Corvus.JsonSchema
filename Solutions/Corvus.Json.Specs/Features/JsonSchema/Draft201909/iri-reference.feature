@draft2019-09

Feature: iri-reference draft2019-09
    In order to use json-schema
    As a developer
    I want to support iri-reference in draft2019-09

Scenario Outline: validation of IRI References
/* Schema: 
{ "format": "iri-reference" }
*/
    Given the input JSON file "optional/format/iri-reference.json"
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
        | #/000/tests/006/data | true  | a valid IRI                                                                      |
        | #/000/tests/007/data | true  | a valid protocol-relative IRI Reference                                          |
        | #/000/tests/008/data | true  | a valid relative IRI Reference                                                   |
        | #/000/tests/009/data | false | an invalid IRI Reference                                                         |
        | #/000/tests/010/data | true  | a valid IRI Reference                                                            |
        | #/000/tests/011/data | true  | a valid IRI fragment                                                             |
        | #/000/tests/012/data | false | an invalid IRI fragment                                                          |
