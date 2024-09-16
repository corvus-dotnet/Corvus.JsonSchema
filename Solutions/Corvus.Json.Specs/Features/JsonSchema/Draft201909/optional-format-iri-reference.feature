@draft2019-09

Feature: optional-format-iri-reference draft2019-09
    In order to use json-schema
    As a developer
    I want to support optional-format-iri-reference in draft2019-09

Scenario Outline: validation of IRI References
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "iri-reference"
        }
*/
    Given the input JSON file "optional/format/iri-reference.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        # http://ƒøø.ßår/?∂éœ=πîx#πîüx
        | #/000/tests/006/data | true  | a valid IRI                                                                      |
        # //ƒøø.ßår/?∂éœ=πîx#πîüx
        | #/000/tests/007/data | true  | a valid protocol-relative IRI Reference                                          |
        # /âππ
        | #/000/tests/008/data | true  | a valid relative IRI Reference                                                   |
        # \\WINDOWS\filëßåré
        | #/000/tests/009/data | false | an invalid IRI Reference                                                         |
        # âππ
        | #/000/tests/010/data | true  | a valid IRI Reference                                                            |
        # #ƒrägmênt
        | #/000/tests/011/data | true  | a valid IRI fragment                                                             |
        # #ƒräg\mênt
        | #/000/tests/012/data | false | an invalid IRI fragment                                                          |
