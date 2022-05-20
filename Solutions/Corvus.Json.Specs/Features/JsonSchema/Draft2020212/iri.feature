@draft2020-12

Feature: iri draft2020-12
    In order to use json-schema
    As a developer
    I want to support iri in draft2020-12

Scenario Outline: validation of IRIs
/* Schema: 
{ "format": "iri" }
*/
    Given the input JSON file "optional\format\iri.json"
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
        | #/000/tests/006/data | true  | a valid IRI with anchor tag                                                      |
        | #/000/tests/007/data | true  | a valid IRI with anchor tag and parentheses                                      |
        | #/000/tests/008/data | true  | a valid IRI with URL-encoded stuff                                               |
        | #/000/tests/009/data | true  | a valid IRI with many special characters                                         |
        | #/000/tests/010/data | true  | a valid IRI based on IPv6                                                        |
        | #/000/tests/011/data | false | an invalid IRI based on IPv6                                                     |
        | #/000/tests/012/data | false | an invalid relative IRI Reference                                                |
        | #/000/tests/013/data | false | an invalid IRI                                                                   |
        | #/000/tests/014/data | false | an invalid IRI though valid IRI reference                                        |
