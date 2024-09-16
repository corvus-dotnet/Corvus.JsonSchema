@openApi30

Feature: maxProperties openApi30
    In order to use json-schema
    As a developer
    I want to support maxProperties in openApi30

Scenario Outline: maxProperties validation
/* Schema: 
{"maxProperties": 2}
*/
    Given the input JSON file "maxProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/000/tests/000/data | true  | shorter is valid                                                                 |
        # {"foo": 1, "bar": 2}
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        # {"foo": 1, "bar": 2, "baz": 3}
        | #/000/tests/002/data | false | too long is invalid                                                              |
        # [1, 2, 3]
        | #/000/tests/003/data | true  | ignores arrays                                                                   |
        # foobar
        | #/000/tests/004/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/005/data | true  | ignores other non-objects                                                        |

Scenario Outline: maxProperties  equals  0 means the object is empty
/* Schema: 
{ "maxProperties": 0 }
*/
    Given the input JSON file "maxProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/001/tests/000/data | true  | no properties is valid                                                           |
        # { "foo": 1 }
        | #/001/tests/001/data | false | one property is invalid                                                          |
