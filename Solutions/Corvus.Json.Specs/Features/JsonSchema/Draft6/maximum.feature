@draft6

Feature: maximum draft6
    In order to use json-schema
    As a developer
    I want to support maximum in draft6

Scenario Outline: maximum validation
/* Schema: 
{"maximum": 3.0}
*/
    Given the input JSON file "maximum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 2.6
        | #/000/tests/000/data | true  | below the maximum is valid                                                       |
        # 3.0
        | #/000/tests/001/data | true  | boundary point is valid                                                          |
        # 3.5
        | #/000/tests/002/data | false | above the maximum is invalid                                                     |
        # x
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |

Scenario Outline: maximum validation with unsigned integer
/* Schema: 
{"maximum": 300}
*/
    Given the input JSON file "maximum.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 299.97
        | #/001/tests/000/data | true  | below the maximum is invalid                                                     |
        # 300
        | #/001/tests/001/data | true  | boundary point integer is valid                                                  |
        # 300.00
        | #/001/tests/002/data | true  | boundary point float is valid                                                    |
        # 300.5
        | #/001/tests/003/data | false | above the maximum is invalid                                                     |
