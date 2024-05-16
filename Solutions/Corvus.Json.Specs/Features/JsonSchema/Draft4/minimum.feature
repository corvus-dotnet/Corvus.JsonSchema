@draft4

Feature: minimum draft4
    In order to use json-schema
    As a developer
    I want to support minimum in draft4

Scenario Outline: minimum validation
/* Schema: 
{"minimum": 1.1}
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 2.6
        | #/000/tests/000/data | true  | above the minimum is valid                                                       |
        # 1.1
        | #/000/tests/001/data | true  | boundary point is valid                                                          |
        # 0.6
        | #/000/tests/002/data | false | below the minimum is invalid                                                     |
        # x
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |

Scenario Outline: minimum validation (explicit false exclusivity)
/* Schema: 
{"minimum": 1.1, "exclusiveMinimum": false}
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 2.6
        | #/001/tests/000/data | true  | above the minimum is valid                                                       |
        # 1.1
        | #/001/tests/001/data | true  | boundary point is valid                                                          |
        # 0.6
        | #/001/tests/002/data | false | below the minimum is invalid                                                     |
        # x
        | #/001/tests/003/data | true  | ignores non-numbers                                                              |

Scenario Outline: exclusiveMinimum validation
/* Schema: 
{
            "minimum": 1.1,
            "exclusiveMinimum": true
        }
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1.2
        | #/002/tests/000/data | true  | above the minimum is still valid                                                 |
        # 1.1
        | #/002/tests/001/data | false | boundary point is invalid                                                        |

Scenario Outline: minimum validation with signed integer
/* Schema: 
{"minimum": -2}
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # -1
        | #/003/tests/000/data | true  | negative above the minimum is valid                                              |
        # 0
        | #/003/tests/001/data | true  | positive above the minimum is valid                                              |
        # -2
        | #/003/tests/002/data | true  | boundary point is valid                                                          |
        # -2.0
        | #/003/tests/003/data | true  | boundary point with float is valid                                               |
        # -2.0001
        | #/003/tests/004/data | false | float below the minimum is invalid                                               |
        # -3
        | #/003/tests/005/data | false | int below the minimum is invalid                                                 |
        # x
        | #/003/tests/006/data | true  | ignores non-numbers                                                              |
