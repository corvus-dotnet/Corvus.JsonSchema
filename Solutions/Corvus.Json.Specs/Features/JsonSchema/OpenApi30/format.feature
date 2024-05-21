@openApi30

Feature: format openApi30
    In order to use json-schema
    As a developer
    I want to support format in openApi30

Scenario Outline: email format
/* Schema: 
{ "format": "email" }
*/
    Given the input JSON file "format.json"
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

Scenario Outline: ipv4 format
/* Schema: 
{ "format": "ipv4" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/001/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/001/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/001/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/001/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/001/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/001/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: ipv6 format
/* Schema: 
{ "format": "ipv6" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/002/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/002/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/002/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/002/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/002/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/002/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: hostname format
/* Schema: 
{ "format": "hostname" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/003/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/003/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/003/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/003/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/003/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/003/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: date-time format
/* Schema: 
{ "format": "date-time" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/004/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/004/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/004/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/004/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/004/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/004/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: uri format
/* Schema: 
{ "format": "uri" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/005/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/005/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/005/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/005/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/005/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/005/tests/005/data | true  | all string formats ignore nulls                                                  |
