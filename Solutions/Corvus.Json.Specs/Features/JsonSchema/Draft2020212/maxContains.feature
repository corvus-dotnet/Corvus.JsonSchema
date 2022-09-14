@draft2020-12

Feature: maxContains draft2020-12
    In order to use json-schema
    As a developer
    I want to support maxContains in draft2020-12

Scenario Outline: maxContains without contains is ignored
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "maxContains": 1
        }
*/
    Given the input JSON file "maxContains.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | one item valid against lone maxContains                                          |
        | #/000/tests/001/data | true  | two items still valid against lone maxContains                                   |

Scenario Outline: maxContains with contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contains": {"const": 1},
            "maxContains": 1
        }
*/
    Given the input JSON file "maxContains.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | false | empty data                                                                       |
        | #/001/tests/001/data | true  | all elements match, valid maxContains                                            |
        | #/001/tests/002/data | false | all elements match, invalid maxContains                                          |
        | #/001/tests/003/data | true  | some elements match, valid maxContains                                           |
        | #/001/tests/004/data | false | some elements match, invalid maxContains                                         |

Scenario Outline: maxContains with contains, value with a decimal
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contains": {"const": 1},
            "maxContains": 1.0
        }
*/
    Given the input JSON file "maxContains.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | one element matches, valid maxContains                                           |
        | #/002/tests/001/data | false | too many elements match, invalid maxContains                                     |

Scenario Outline: minContains  less than  maxContains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contains": {"const": 1},
            "minContains": 1,
            "maxContains": 3
        }
*/
    Given the input JSON file "maxContains.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | false | actual < minContains < maxContains                                               |
        | #/003/tests/001/data | true  | minContains < actual < maxContains                                               |
        | #/003/tests/002/data | false | minContains < maxContains < actual                                               |
