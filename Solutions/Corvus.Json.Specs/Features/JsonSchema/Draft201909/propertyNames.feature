@draft2019-09

Feature: propertyNames draft2019-09
    In order to use json-schema
    As a developer
    I want to support propertyNames in draft2019-09

Scenario Outline: propertyNames validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "propertyNames": {"maxLength": 3}
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | all property names valid                                                         |
        | #/000/tests/001/data | false | some property names invalid                                                      |
        | #/000/tests/002/data | true  | object without properties is valid                                               |
        | #/000/tests/003/data | true  | ignores arrays                                                                   |
        | #/000/tests/004/data | true  | ignores strings                                                                  |
        | #/000/tests/005/data | true  | ignores other non-objects                                                        |

Scenario Outline: propertyNames validation with pattern
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "propertyNames": { "pattern": "^a+$" }
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | matching property names valid                                                    |
        | #/001/tests/001/data | false | non-matching property name is invalid                                            |
        | #/001/tests/002/data | true  | object without properties is valid                                               |

Scenario Outline: propertyNames with boolean schema true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "propertyNames": true
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | object with any properties is valid                                              |
        | #/002/tests/001/data | true  | empty object is valid                                                            |

Scenario Outline: propertyNames with boolean schema false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "propertyNames": false
        }
*/
    Given the input JSON file "propertyNames.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | false | object with any properties is invalid                                            |
        | #/003/tests/001/data | true  | empty object is valid                                                            |
