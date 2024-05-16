@openApi30

Feature: uniqueItems openApi30
    In order to use json-schema
    As a developer
    I want to support uniqueItems in openApi30

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
        # [1, 2]
        | #/000/tests/000/data | true  | unique array of integers is valid                                                |
        # [1, 1]
        | #/000/tests/001/data | false | non-unique array of integers is invalid                                          |
        # [1, 2, 1]
        | #/000/tests/002/data | false | non-unique array of more than two integers is invalid                            |
        # [1.0, 1.00, 1]
        | #/000/tests/003/data | false | numbers are unique if mathematically unequal                                     |
        # [0, false]
        | #/000/tests/004/data | true  | false is not equal to zero                                                       |
        # [1, true]
        | #/000/tests/005/data | true  | true is not equal to one                                                         |
        # ["foo", "bar", "baz"]
        | #/000/tests/006/data | true  | unique array of strings is valid                                                 |
        # ["foo", "bar", "foo"]
        | #/000/tests/007/data | false | non-unique array of strings is invalid                                           |
        # [{"foo": "bar"}, {"foo": "baz"}]
        | #/000/tests/008/data | true  | unique array of objects is valid                                                 |
        # [{"foo": "bar"}, {"foo": "bar"}]
        | #/000/tests/009/data | false | non-unique array of objects is invalid                                           |
        # [{"foo": "bar", "bar": "foo"}, {"bar": "foo", "foo": "bar"}]
        | #/000/tests/010/data | false | property order of array of objects is ignored                                    |
        # [ {"foo": {"bar" : {"baz" : true}}}, {"foo": {"bar" : {"baz" : false}}} ]
        | #/000/tests/011/data | true  | unique array of nested objects is valid                                          |
        # [ {"foo": {"bar" : {"baz" : true}}}, {"foo": {"bar" : {"baz" : true}}} ]
        | #/000/tests/012/data | false | non-unique array of nested objects is invalid                                    |
        # [["foo"], ["bar"]]
        | #/000/tests/013/data | true  | unique array of arrays is valid                                                  |
        # [["foo"], ["foo"]]
        | #/000/tests/014/data | false | non-unique array of arrays is invalid                                            |
        # [["foo"], ["bar"], ["foo"]]
        | #/000/tests/015/data | false | non-unique array of more than two arrays is invalid                              |
        # [1, true]
        | #/000/tests/016/data | true  | 1 and true are unique                                                            |
        # [0, false]
        | #/000/tests/017/data | true  | 0 and false are unique                                                           |
        # [[1], [true]]
        | #/000/tests/018/data | true  | [1] and [true] are unique                                                        |
        # [[0], [false]]
        | #/000/tests/019/data | true  | [0] and [false] are unique                                                       |
        # [[[1], "foo"], [[true], "foo"]]
        | #/000/tests/020/data | true  | nested [1] and [true] are unique                                                 |
        # [[[0], "foo"], [[false], "foo"]]
        | #/000/tests/021/data | true  | nested [0] and [false] are unique                                                |
        # [{}, [1], true, null, 1, "{}"]
        | #/000/tests/022/data | true  | unique heterogeneous types are valid                                             |
        # [{}, [1], true, null, {}, 1]
        | #/000/tests/023/data | false | non-unique heterogeneous types are invalid                                       |
        # [{"a": 1, "b": 2}, {"a": 2, "b": 1}]
        | #/000/tests/024/data | true  | different objects are unique                                                     |
        # [{"a": 1, "b": 2}, {"b": 2, "a": 1}]
        | #/000/tests/025/data | false | objects are non-unique despite key order                                         |
        # [{"a": false}, {"a": 0}]
        | #/000/tests/026/data | true  | {"a": false} and {"a": 0} are unique                                             |
        # [{"a": true}, {"a": 1}]
        | #/000/tests/027/data | true  | {"a": true} and {"a": 1} are unique                                              |

Scenario Outline: uniqueItems equals false validation
/* Schema: 
{ "uniqueItems": false }
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
        # [1, 2]
        | #/001/tests/000/data | true  | unique array of integers is valid                                                |
        # [1, 1]
        | #/001/tests/001/data | true  | non-unique array of integers is valid                                            |
        # [1.0, 1.00, 1]
        | #/001/tests/002/data | true  | numbers are unique if mathematically unequal                                     |
        # [0, false]
        | #/001/tests/003/data | true  | false is not equal to zero                                                       |
        # [1, true]
        | #/001/tests/004/data | true  | true is not equal to one                                                         |
        # [{"foo": "bar"}, {"foo": "baz"}]
        | #/001/tests/005/data | true  | unique array of objects is valid                                                 |
        # [{"foo": "bar"}, {"foo": "bar"}]
        | #/001/tests/006/data | true  | non-unique array of objects is valid                                             |
        # [ {"foo": {"bar" : {"baz" : true}}}, {"foo": {"bar" : {"baz" : false}}} ]
        | #/001/tests/007/data | true  | unique array of nested objects is valid                                          |
        # [ {"foo": {"bar" : {"baz" : true}}}, {"foo": {"bar" : {"baz" : true}}} ]
        | #/001/tests/008/data | true  | non-unique array of nested objects is valid                                      |
        # [["foo"], ["bar"]]
        | #/001/tests/009/data | true  | unique array of arrays is valid                                                  |
        # [["foo"], ["foo"]]
        | #/001/tests/010/data | true  | non-unique array of arrays is valid                                              |
        # [1, true]
        | #/001/tests/011/data | true  | 1 and true are unique                                                            |
        # [0, false]
        | #/001/tests/012/data | true  | 0 and false are unique                                                           |
        # [{}, [1], true, null, 1]
        | #/001/tests/013/data | true  | unique heterogeneous types are valid                                             |
        # [{}, [1], true, null, {}, 1]
        | #/001/tests/014/data | true  | non-unique heterogeneous types are valid                                         |
