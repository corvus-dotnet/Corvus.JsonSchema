@draft2019-09

Feature: boolean_schema draft2019-09
    In order to use json-schema
    As a developer
    I want to support boolean_schema in draft2019-09

Scenario Outline: boolean schema 'true'
/* Schema: 
True
*/
    Given the input JSON file "boolean_schema.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/000/tests/000/data | true  | number is valid                                                                  |
        # foo
        | #/000/tests/001/data | true  | string is valid                                                                  |
        # True
        | #/000/tests/002/data | true  | boolean true is valid                                                            |
        # False
        | #/000/tests/003/data | true  | boolean false is valid                                                           |
        # 
        | #/000/tests/004/data | true  | null is valid                                                                    |
        # {"foo": "bar"}
        | #/000/tests/005/data | true  | object is valid                                                                  |
        # {}
        | #/000/tests/006/data | true  | empty object is valid                                                            |
        # ["foo"]
        | #/000/tests/007/data | true  | array is valid                                                                   |
        # []
        | #/000/tests/008/data | true  | empty array is valid                                                             |

Scenario Outline: boolean schema 'false'
/* Schema: 
False
*/
    Given the input JSON file "boolean_schema.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/001/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/001/tests/001/data | false | string is invalid                                                                |
        # True
        | #/001/tests/002/data | false | boolean true is invalid                                                          |
        # False
        | #/001/tests/003/data | false | boolean false is invalid                                                         |
        # 
        | #/001/tests/004/data | false | null is invalid                                                                  |
        # {"foo": "bar"}
        | #/001/tests/005/data | false | object is invalid                                                                |
        # {}
        | #/001/tests/006/data | false | empty object is invalid                                                          |
        # ["foo"]
        | #/001/tests/007/data | false | array is invalid                                                                 |
        # []
        | #/001/tests/008/data | false | empty array is invalid                                                           |
