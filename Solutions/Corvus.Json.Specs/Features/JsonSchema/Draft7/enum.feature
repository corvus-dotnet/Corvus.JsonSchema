@draft7

Feature: enum draft7
    In order to use json-schema
    As a developer
    I want to support enum in draft7

Scenario Outline: simple enum validation
/* Schema: 
{"enum": [1, 2, 3]}
*/
    Given the input JSON file "enum.json"
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
        | #/000/tests/000/data | true  | one of the enum is valid                                                         |
        # 4
        | #/000/tests/001/data | false | something else is invalid                                                        |

Scenario Outline: heterogeneous enum validation
/* Schema: 
{"enum": [6, "foo", [], true, {"foo": 12}]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # []
        | #/001/tests/000/data | true  | one of the enum is valid                                                         |
        # 
        | #/001/tests/001/data | false | something else is invalid                                                        |
        # {"foo": false}
        | #/001/tests/002/data | false | objects are deep compared                                                        |
        # {"foo": 12}
        | #/001/tests/003/data | true  | valid object matches                                                             |
        # {"foo": 12, "boo": 42}
        | #/001/tests/004/data | false | extra properties in object is invalid                                            |

Scenario Outline: heterogeneous enum-with-null validation
/* Schema: 
{ "enum": [6, null] }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 
        | #/002/tests/000/data | true  | null is valid                                                                    |
        # 6
        | #/002/tests/001/data | true  | number is valid                                                                  |
        # test
        | #/002/tests/002/data | false | something else is invalid                                                        |

Scenario Outline: enums in properties
/* Schema: 
{
            "type":"object",
            "properties": {
                "foo": {"enum":["foo"]},
                "bar": {"enum":["bar"]}
            },
            "required": ["bar"]
        }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo":"foo", "bar":"bar"}
        | #/003/tests/000/data | true  | both properties are valid                                                        |
        # {"foo":"foot", "bar":"bar"}
        | #/003/tests/001/data | false | wrong foo value                                                                  |
        # {"foo":"foo", "bar":"bart"}
        | #/003/tests/002/data | false | wrong bar value                                                                  |
        # {"bar":"bar"}
        | #/003/tests/003/data | true  | missing optional property is valid                                               |
        # {"foo":"foo"}
        | #/003/tests/004/data | false | missing required property is invalid                                             |
        # {}
        | #/003/tests/005/data | false | missing all properties is invalid                                                |

Scenario Outline: enum with escaped characters
/* Schema: 
{
            "enum": ["foo\nbar", "foo\rbar"]
        }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo bar
        | #/004/tests/000/data | true  | member 1 is valid                                                                |
        # foo bar
        | #/004/tests/001/data | true  | member 2 is valid                                                                |
        # abc
        | #/004/tests/002/data | false | another string is invalid                                                        |

Scenario Outline: enum with false does not match 0
/* Schema: 
{"enum": [false]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # False
        | #/005/tests/000/data | true  | false is valid                                                                   |
        # 0
        | #/005/tests/001/data | false | integer zero is invalid                                                          |
        # 0.0
        | #/005/tests/002/data | false | float zero is invalid                                                            |

Scenario Outline: enum with array[false] does not match array[0]
/* Schema: 
{"enum": [[false]]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [false]
        | #/006/tests/000/data | true  | [false] is valid                                                                 |
        # [0]
        | #/006/tests/001/data | false | [0] is invalid                                                                   |
        # [0.0]
        | #/006/tests/002/data | false | [0.0] is invalid                                                                 |

Scenario Outline: enum with true does not match 1
/* Schema: 
{"enum": [true]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # True
        | #/007/tests/000/data | true  | true is valid                                                                    |
        # 1
        | #/007/tests/001/data | false | integer one is invalid                                                           |
        # 1.0
        | #/007/tests/002/data | false | float one is invalid                                                             |

Scenario Outline: enum with array[true] does not match array[1]
/* Schema: 
{"enum": [[true]]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [true]
        | #/008/tests/000/data | true  | [true] is valid                                                                  |
        # [1]
        | #/008/tests/001/data | false | [1] is invalid                                                                   |
        # [1.0]
        | #/008/tests/002/data | false | [1.0] is invalid                                                                 |

Scenario Outline: enum with 0 does not match false
/* Schema: 
{"enum": [0]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # False
        | #/009/tests/000/data | false | false is invalid                                                                 |
        # 0
        | #/009/tests/001/data | true  | integer zero is valid                                                            |
        # 0.0
        | #/009/tests/002/data | true  | float zero is valid                                                              |

Scenario Outline: enum with array[0] does not match array[false]
/* Schema: 
{"enum": [[0]]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [false]
        | #/010/tests/000/data | false | [false] is invalid                                                               |
        # [0]
        | #/010/tests/001/data | true  | [0] is valid                                                                     |
        # [0.0]
        | #/010/tests/002/data | true  | [0.0] is valid                                                                   |

Scenario Outline: enum with 1 does not match true
/* Schema: 
{"enum": [1]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # True
        | #/011/tests/000/data | false | true is invalid                                                                  |
        # 1
        | #/011/tests/001/data | true  | integer one is valid                                                             |
        # 1.0
        | #/011/tests/002/data | true  | float one is valid                                                               |

Scenario Outline: enum with array[1] does not match array[true]
/* Schema: 
{"enum": [[1]]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [true]
        | #/012/tests/000/data | false | [true] is invalid                                                                |
        # [1]
        | #/012/tests/001/data | true  | [1] is valid                                                                     |
        # [1.0]
        | #/012/tests/002/data | true  | [1.0] is valid                                                                   |

Scenario Outline: nul characters in strings
/* Schema: 
{ "enum": [ "hello\u0000there" ] }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # hello there
        | #/013/tests/000/data | true  | match string with nul                                                            |
        # hellothere
        | #/013/tests/001/data | false | do not match string lacking nul                                                  |
