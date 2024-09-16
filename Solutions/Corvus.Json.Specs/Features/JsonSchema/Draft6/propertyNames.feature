@draft6

Feature: propertyNames draft6
    In order to use json-schema
    As a developer
    I want to support propertyNames in draft6

Scenario Outline: propertyNames validation
/* Schema: 
{
            "propertyNames": {"maxLength": 3}
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "f": {}, "foo": {} }
        | #/000/tests/000/data | true  | all property names valid                                                         |
        # { "foo": {}, "foobar": {} }
        | #/000/tests/001/data | false | some property names invalid                                                      |
        # {}
        | #/000/tests/002/data | true  | object without properties is valid                                               |
        # [1, 2, 3, 4]
        | #/000/tests/003/data | true  | ignores arrays                                                                   |
        # foobar
        | #/000/tests/004/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/005/data | true  | ignores other non-objects                                                        |

Scenario Outline: propertyNames validation with pattern
/* Schema: 
{
            "propertyNames": { "pattern": "^a+$" }
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "a": {}, "aa": {}, "aaa": {} }
        | #/001/tests/000/data | true  | matching property names valid                                                    |
        # { "aaA": {} }
        | #/001/tests/001/data | false | non-matching property name is invalid                                            |
        # {}
        | #/001/tests/002/data | true  | object without properties is valid                                               |

Scenario Outline: propertyNames with boolean schema true
/* Schema: 
{"propertyNames": true}
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/002/tests/000/data | true  | object with any properties is valid                                              |
        # {}
        | #/002/tests/001/data | true  | empty object is valid                                                            |

Scenario Outline: propertyNames with boolean schema false
/* Schema: 
{"propertyNames": false}
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/003/tests/000/data | false | object with any properties is invalid                                            |
        # {}
        | #/003/tests/001/data | true  | empty object is valid                                                            |
