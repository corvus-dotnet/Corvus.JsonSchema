@draft2020-12

Feature: relative-json-pointer draft2020-12
    In order to use json-schema
    As a developer
    I want to support relative-json-pointer in draft2020-12

Scenario Outline: validation of Relative JSON Pointers (RJP)
/* Schema: 
{"format": "relative-json-pointer"}
*/
    Given the input JSON file "relative-json-pointer.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid upwards RJP                                                              |
        | #/000/tests/001/data | true  | a valid downwards RJP                                                            |
        | #/000/tests/002/data | true  | a valid up and then down RJP, with array index                                   |
        | #/000/tests/003/data | true  | a valid RJP taking the member or index name                                      |
        | #/000/tests/004/data | false | an invalid RJP that is a valid JSON Pointer                                      |
        | #/000/tests/005/data | false | negative prefix                                                                  |
        | #/000/tests/006/data | false | ## is not a valid json-pointer                                                   |
        | #/000/tests/007/data | false | zero cannot be followed by other digits, plus json-pointer                       |
        | #/000/tests/008/data | false | zero cannot be followed by other digits, plus octothorpe                         |
