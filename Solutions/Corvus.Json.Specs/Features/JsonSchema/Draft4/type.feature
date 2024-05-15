@draft4

Feature: type draft4
    In order to use json-schema
    As a developer
    I want to support type in draft4

Scenario Outline: integer type matches integers
/* Schema: 
{"type": "integer"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/000/tests/000/data | true  | an integer is an integer                                                         |
        # 1.1
        | #/000/tests/001/data | false | a float is not an integer                                                        |
        # foo
        | #/000/tests/002/data | false | a string is not an integer                                                       |
        # 1
        | #/000/tests/003/data | false | a string is still not an integer, even if it looks like one                      |
        # {}
        | #/000/tests/004/data | false | an object is not an integer                                                      |
        # []
        | #/000/tests/005/data | false | an array is not an integer                                                       |
        # True
        | #/000/tests/006/data | false | a boolean is not an integer                                                      |
        # 
        | #/000/tests/007/data | false | null is not an integer                                                           |

Scenario Outline: number type matches numbers
/* Schema: 
{"type": "number"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/001/tests/000/data | true  | an integer is a number                                                           |
        # 1.0
        | #/001/tests/001/data | true  | a float with zero fractional part is a number                                    |
        # 1.1
        | #/001/tests/002/data | true  | a float is a number                                                              |
        # foo
        | #/001/tests/003/data | false | a string is not a number                                                         |
        # 1
        | #/001/tests/004/data | false | a string is still not a number, even if it looks like one                        |
        # {}
        | #/001/tests/005/data | false | an object is not a number                                                        |
        # []
        | #/001/tests/006/data | false | an array is not a number                                                         |
        # True
        | #/001/tests/007/data | false | a boolean is not a number                                                        |
        # 
        | #/001/tests/008/data | false | null is not a number                                                             |

Scenario Outline: string type matches strings
/* Schema: 
{"type": "string"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/002/tests/000/data | false | 1 is not a string                                                                |
        # 1.1
        | #/002/tests/001/data | false | a float is not a string                                                          |
        # foo
        | #/002/tests/002/data | true  | a string is a string                                                             |
        # 1
        | #/002/tests/003/data | true  | a string is still a string, even if it looks like a number                       |
        # 
        | #/002/tests/004/data | true  | an empty string is still a string                                                |
        # {}
        | #/002/tests/005/data | false | an object is not a string                                                        |
        # []
        | #/002/tests/006/data | false | an array is not a string                                                         |
        # True
        | #/002/tests/007/data | false | a boolean is not a string                                                        |
        # 
        | #/002/tests/008/data | false | null is not a string                                                             |

Scenario Outline: object type matches objects
/* Schema: 
{"type": "object"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/003/tests/000/data | false | an integer is not an object                                                      |
        # 1.1
        | #/003/tests/001/data | false | a float is not an object                                                         |
        # foo
        | #/003/tests/002/data | false | a string is not an object                                                        |
        # {}
        | #/003/tests/003/data | true  | an object is an object                                                           |
        # []
        | #/003/tests/004/data | false | an array is not an object                                                        |
        # True
        | #/003/tests/005/data | false | a boolean is not an object                                                       |
        # 
        | #/003/tests/006/data | false | null is not an object                                                            |

Scenario Outline: array type matches arrays
/* Schema: 
{"type": "array"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/004/tests/000/data | false | an integer is not an array                                                       |
        # 1.1
        | #/004/tests/001/data | false | a float is not an array                                                          |
        # foo
        | #/004/tests/002/data | false | a string is not an array                                                         |
        # {}
        | #/004/tests/003/data | false | an object is not an array                                                        |
        # []
        | #/004/tests/004/data | true  | an array is an array                                                             |
        # True
        | #/004/tests/005/data | false | a boolean is not an array                                                        |
        # 
        | #/004/tests/006/data | false | null is not an array                                                             |

Scenario Outline: boolean type matches booleans
/* Schema: 
{"type": "boolean"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/005/tests/000/data | false | an integer is not a boolean                                                      |
        # 0
        | #/005/tests/001/data | false | zero is not a boolean                                                            |
        # 1.1
        | #/005/tests/002/data | false | a float is not a boolean                                                         |
        # foo
        | #/005/tests/003/data | false | a string is not a boolean                                                        |
        # 
        | #/005/tests/004/data | false | an empty string is not a boolean                                                 |
        # {}
        | #/005/tests/005/data | false | an object is not a boolean                                                       |
        # []
        | #/005/tests/006/data | false | an array is not a boolean                                                        |
        # True
        | #/005/tests/007/data | true  | true is a boolean                                                                |
        # False
        | #/005/tests/008/data | true  | false is a boolean                                                               |
        # 
        | #/005/tests/009/data | false | null is not a boolean                                                            |

Scenario Outline: null type matches only the null object
/* Schema: 
{"type": "null"}
*/
    Given the input JSON file "type.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/006/tests/000/data | false | an integer is not null                                                           |
        # 1.1
        | #/006/tests/001/data | false | a float is not null                                                              |
        # 0
        | #/006/tests/002/data | false | zero is not null                                                                 |
        # foo
        | #/006/tests/003/data | false | a string is not null                                                             |
        # 
        | #/006/tests/004/data | false | an empty string is not null                                                      |
        # {}
        | #/006/tests/005/data | false | an object is not null                                                            |
        # []
        | #/006/tests/006/data | false | an array is not null                                                             |
        # True
        | #/006/tests/007/data | false | true is not null                                                                 |
        # False
        | #/006/tests/008/data | false | false is not null                                                                |
        # 
        | #/006/tests/009/data | true  | null is null                                                                     |

Scenario Outline: multiple types can be specified in an array
/* Schema: 
{"type": ["integer", "string"]}
*/
    Given the input JSON file "type.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/007/tests/000/data | true  | an integer is valid                                                              |
        # foo
        | #/007/tests/001/data | true  | a string is valid                                                                |
        # 1.1
        | #/007/tests/002/data | false | a float is invalid                                                               |
        # {}
        | #/007/tests/003/data | false | an object is invalid                                                             |
        # []
        | #/007/tests/004/data | false | an array is invalid                                                              |
        # True
        | #/007/tests/005/data | false | a boolean is invalid                                                             |
        # 
        | #/007/tests/006/data | false | null is invalid                                                                  |

Scenario Outline: type as array with one item
/* Schema: 
{
            "type": ["string"]
        }
*/
    Given the input JSON file "type.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/008/tests/000/data | true  | string is valid                                                                  |
        # 123
        | #/008/tests/001/data | false | number is invalid                                                                |

Scenario Outline: type: array or object
/* Schema: 
{
            "type": ["array", "object"]
        }
*/
    Given the input JSON file "type.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1,2,3]
        | #/009/tests/000/data | true  | array is valid                                                                   |
        # {"foo": 123}
        | #/009/tests/001/data | true  | object is valid                                                                  |
        # 123
        | #/009/tests/002/data | false | number is invalid                                                                |
        # foo
        | #/009/tests/003/data | false | string is invalid                                                                |
        # 
        | #/009/tests/004/data | false | null is invalid                                                                  |

Scenario Outline: type: array, object or null
/* Schema: 
{
            "type": ["array", "object", "null"]
        }
*/
    Given the input JSON file "type.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1,2,3]
        | #/010/tests/000/data | true  | array is valid                                                                   |
        # {"foo": 123}
        | #/010/tests/001/data | true  | object is valid                                                                  |
        # 
        | #/010/tests/002/data | true  | null is valid                                                                    |
        # 123
        | #/010/tests/003/data | false | number is invalid                                                                |
        # foo
        | #/010/tests/004/data | false | string is invalid                                                                |
