@draft2020-12

Feature: iri-reference draft2020-12
    In order to use json-schema
    As a developer
    I want to support iri-reference in draft2020-12

Scenario Outline: validation of IRI References
/* Schema: 
{"format": "iri-reference"}
*/
    Given the input JSON file "iri-reference.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid IRI                                                                      |
        | #/000/tests/001/data | true  | a valid protocol-relative IRI Reference                                          |
        | #/000/tests/002/data | true  | a valid relative IRI Reference                                                   |
        | #/000/tests/003/data | false | an invalid IRI Reference                                                         |
        | #/000/tests/004/data | true  | a valid IRI Reference                                                            |
        | #/000/tests/005/data | true  | a valid IRI fragment                                                             |
        | #/000/tests/006/data | false | an invalid IRI fragment                                                          |
