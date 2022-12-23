@draft2019-09

Feature: minContains draft2019-09
    In order to use json-schema
    As a developer
    I want to support minContains in draft2019-09

Scenario Outline: minContains without contains is ignored
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "minContains": 1
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | one item valid against lone minContains                                          |
        | #/000/tests/001/data | true  | zero items still valid against lone minContains                                  |

Scenario Outline: minContains equals 1 with contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "minContains": 1
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | false | empty data                                                                       |
        | #/001/tests/001/data | false | no elements match                                                                |
        | #/001/tests/002/data | true  | single element matches, valid minContains                                        |
        | #/001/tests/003/data | true  | some elements match, valid minContains                                           |
        | #/001/tests/004/data | true  | all elements match, valid minContains                                            |

Scenario Outline: minContains equals 2 with contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "minContains": 2
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | false | empty data                                                                       |
        | #/002/tests/001/data | false | all elements match, invalid minContains                                          |
        | #/002/tests/002/data | false | some elements match, invalid minContains                                         |
        | #/002/tests/003/data | true  | all elements match, valid minContains (exactly as needed)                        |
        | #/002/tests/004/data | true  | all elements match, valid minContains (more than needed)                         |
        | #/002/tests/005/data | true  | some elements match, valid minContains                                           |

Scenario Outline: minContains equals 2 with contains with a decimal value
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "minContains": 2.0
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | false | one element matches, invalid minContains                                         |
        | #/003/tests/001/data | true  | both elements match, valid minContains                                           |

Scenario Outline: maxContains  equals  minContains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "maxContains": 2,
            "minContains": 2
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | false | empty data                                                                       |
        | #/004/tests/001/data | false | all elements match, invalid minContains                                          |
        | #/004/tests/002/data | false | all elements match, invalid maxContains                                          |
        | #/004/tests/003/data | true  | all elements match, valid maxContains and minContains                            |

Scenario Outline: maxContains  less than  minContains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "maxContains": 1,
            "minContains": 3
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | false | empty data                                                                       |
        | #/005/tests/001/data | false | invalid minContains                                                              |
        | #/005/tests/002/data | false | invalid maxContains                                                              |
        | #/005/tests/003/data | false | invalid maxContains and minContains                                              |

Scenario Outline: minContains  equals  0 with no maxContains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "minContains": 0
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | empty data                                                                       |
        | #/006/tests/001/data | true  | minContains = 0 makes contains always pass                                       |

Scenario Outline: minContains  equals  0 with maxContains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"const": 1},
            "minContains": 0,
            "maxContains": 1
        }
*/
    Given the input JSON file "minContains.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | empty data                                                                       |
        | #/007/tests/001/data | true  | not more than maxContains                                                        |
        | #/007/tests/002/data | false | too many                                                                         |
