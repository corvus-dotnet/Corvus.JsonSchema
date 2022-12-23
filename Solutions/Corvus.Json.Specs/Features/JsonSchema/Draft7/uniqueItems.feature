@draft7

Feature: uniqueItems draft7
    In order to use json-schema
    As a developer
    I want to support uniqueItems in draft7

Scenario Outline: uniqueItems validation
/* Schema: 
{"uniqueItems": true}
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | unique array of integers is valid                                                |
        | #/000/tests/001/data | false | non-unique array of integers is invalid                                          |
        | #/000/tests/002/data | false | non-unique array of more than two integers is invalid                            |
        | #/000/tests/003/data | false | numbers are unique if mathematically unequal                                     |
        | #/000/tests/004/data | true  | false is not equal to zero                                                       |
        | #/000/tests/005/data | true  | true is not equal to one                                                         |
        | #/000/tests/006/data | true  | unique array of strings is valid                                                 |
        | #/000/tests/007/data | false | non-unique array of strings is invalid                                           |
        | #/000/tests/008/data | true  | unique array of objects is valid                                                 |
        | #/000/tests/009/data | false | non-unique array of objects is invalid                                           |
        | #/000/tests/010/data | true  | unique array of nested objects is valid                                          |
        | #/000/tests/011/data | false | non-unique array of nested objects is invalid                                    |
        | #/000/tests/012/data | true  | unique array of arrays is valid                                                  |
        | #/000/tests/013/data | false | non-unique array of arrays is invalid                                            |
        | #/000/tests/014/data | false | non-unique array of more than two arrays is invalid                              |
        | #/000/tests/015/data | true  | 1 and true are unique                                                            |
        | #/000/tests/016/data | true  | 0 and false are unique                                                           |
        | #/000/tests/017/data | true  | [1] and [true] are unique                                                        |
        | #/000/tests/018/data | true  | [0] and [false] are unique                                                       |
        | #/000/tests/019/data | true  | nested [1] and [true] are unique                                                 |
        | #/000/tests/020/data | true  | nested [0] and [false] are unique                                                |
        | #/000/tests/021/data | true  | unique heterogeneous types are valid                                             |
        | #/000/tests/022/data | false | non-unique heterogeneous types are invalid                                       |
        | #/000/tests/023/data | true  | different objects are unique                                                     |
        | #/000/tests/024/data | false | objects are non-unique despite key order                                         |
        | #/000/tests/025/data | true  | {"a": false} and {"a": 0} are unique                                             |
        | #/000/tests/026/data | true  | {"a": true} and {"a": 1} are unique                                              |

Scenario Outline: uniqueItems with an array of items
/* Schema: 
{
            "items": [{"type": "boolean"}, {"type": "boolean"}],
            "uniqueItems": true
        }
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | [false, true] from items array is valid                                          |
        | #/001/tests/001/data | true  | [true, false] from items array is valid                                          |
        | #/001/tests/002/data | false | [false, false] from items array is not valid                                     |
        | #/001/tests/003/data | false | [true, true] from items array is not valid                                       |
        | #/001/tests/004/data | true  | unique array extended from [false, true] is valid                                |
        | #/001/tests/005/data | true  | unique array extended from [true, false] is valid                                |
        | #/001/tests/006/data | false | non-unique array extended from [false, true] is not valid                        |
        | #/001/tests/007/data | false | non-unique array extended from [true, false] is not valid                        |

Scenario Outline: uniqueItems with an array of items and additionalItems equals false
/* Schema: 
{
            "items": [{"type": "boolean"}, {"type": "boolean"}],
            "uniqueItems": true,
            "additionalItems": false
        }
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | [false, true] from items array is valid                                          |
        | #/002/tests/001/data | true  | [true, false] from items array is valid                                          |
        | #/002/tests/002/data | false | [false, false] from items array is not valid                                     |
        | #/002/tests/003/data | false | [true, true] from items array is not valid                                       |
        | #/002/tests/004/data | false | extra items are invalid even if unique                                           |

Scenario Outline: uniqueItems equals false validation
/* Schema: 
{ "uniqueItems": false }
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | unique array of integers is valid                                                |
        | #/003/tests/001/data | true  | non-unique array of integers is valid                                            |
        | #/003/tests/002/data | true  | numbers are unique if mathematically unequal                                     |
        | #/003/tests/003/data | true  | false is not equal to zero                                                       |
        | #/003/tests/004/data | true  | true is not equal to one                                                         |
        | #/003/tests/005/data | true  | unique array of objects is valid                                                 |
        | #/003/tests/006/data | true  | non-unique array of objects is valid                                             |
        | #/003/tests/007/data | true  | unique array of nested objects is valid                                          |
        | #/003/tests/008/data | true  | non-unique array of nested objects is valid                                      |
        | #/003/tests/009/data | true  | unique array of arrays is valid                                                  |
        | #/003/tests/010/data | true  | non-unique array of arrays is valid                                              |
        | #/003/tests/011/data | true  | 1 and true are unique                                                            |
        | #/003/tests/012/data | true  | 0 and false are unique                                                           |
        | #/003/tests/013/data | true  | unique heterogeneous types are valid                                             |
        | #/003/tests/014/data | true  | non-unique heterogeneous types are valid                                         |

Scenario Outline: uniqueItems equals false with an array of items
/* Schema: 
{
            "items": [{"type": "boolean"}, {"type": "boolean"}],
            "uniqueItems": false
        }
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | [false, true] from items array is valid                                          |
        | #/004/tests/001/data | true  | [true, false] from items array is valid                                          |
        | #/004/tests/002/data | true  | [false, false] from items array is valid                                         |
        | #/004/tests/003/data | true  | [true, true] from items array is valid                                           |
        | #/004/tests/004/data | true  | unique array extended from [false, true] is valid                                |
        | #/004/tests/005/data | true  | unique array extended from [true, false] is valid                                |
        | #/004/tests/006/data | true  | non-unique array extended from [false, true] is valid                            |
        | #/004/tests/007/data | true  | non-unique array extended from [true, false] is valid                            |

Scenario Outline: uniqueItems equals false with an array of items and additionalItems equals false
/* Schema: 
{
            "items": [{"type": "boolean"}, {"type": "boolean"}],
            "uniqueItems": false,
            "additionalItems": false
        }
*/
    Given the input JSON file "uniqueItems.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | [false, true] from items array is valid                                          |
        | #/005/tests/001/data | true  | [true, false] from items array is valid                                          |
        | #/005/tests/002/data | true  | [false, false] from items array is valid                                         |
        | #/005/tests/003/data | true  | [true, true] from items array is valid                                           |
        | #/005/tests/004/data | false | extra items are invalid even if unique                                           |
