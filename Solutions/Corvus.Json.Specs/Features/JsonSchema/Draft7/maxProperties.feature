@draft7

Feature: maxProperties draft7
    In order to use json-schema
    As a developer
    I want to support maxProperties in draft7

Scenario Outline: maxProperties validation
/* Schema: 
{"maxProperties": 2}
*/
    Given the input JSON file "maxProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | shorter is valid                                                                 |
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        | #/000/tests/002/data | false | too long is invalid                                                              |
        | #/000/tests/003/data | true  | ignores arrays                                                                   |
        | #/000/tests/004/data | true  | ignores strings                                                                  |
        | #/000/tests/005/data | true  | ignores other non-objects                                                        |

Scenario Outline: maxProperties validation with a decimal
/* Schema: 
{"maxProperties": 2.0}
*/
    Given the input JSON file "maxProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | shorter is valid                                                                 |
        | #/001/tests/001/data | false | too long is invalid                                                              |

Scenario Outline: maxProperties  equals  0 means the object is empty
/* Schema: 
{ "maxProperties": 0 }
*/
    Given the input JSON file "maxProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | no properties is valid                                                           |
        | #/002/tests/001/data | false | one property is invalid                                                          |
