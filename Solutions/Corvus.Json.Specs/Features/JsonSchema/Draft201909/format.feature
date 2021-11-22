@draft2019-09

Feature: format draft2019-09
    In order to use json-schema
    As a developer
    I want to support format in draft2019-09

Scenario Outline: validation of e-mail addresses
/* Schema: 
{"format": "email"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | ignores integers                                                                 |
        | #/000/tests/001/data | true  | ignores floats                                                                   |
        | #/000/tests/002/data | true  | ignores objects                                                                  |
        | #/000/tests/003/data | true  | ignores arrays                                                                   |
        | #/000/tests/004/data | true  | ignores booleans                                                                 |
        | #/000/tests/005/data | true  | ignores null                                                                     |
        #| #/000/tests/006/data | true  | invalid email string is only an annotation by default                            |

Scenario Outline: validation of IDN e-mail addresses
/* Schema: 
{"format": "idn-email"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | ignores integers                                                                 |
        | #/001/tests/001/data | true  | ignores floats                                                                   |
        | #/001/tests/002/data | true  | ignores objects                                                                  |
        | #/001/tests/003/data | true  | ignores arrays                                                                   |
        | #/001/tests/004/data | true  | ignores booleans                                                                 |
        | #/001/tests/005/data | true  | ignores null                                                                     |
        #| #/001/tests/006/data | true  | invalid idn-email string is only an annotation by default                        |

Scenario Outline: validation of regexes
/* Schema: 
{"format": "regex"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | ignores integers                                                                 |
        | #/002/tests/001/data | true  | ignores floats                                                                   |
        | #/002/tests/002/data | true  | ignores objects                                                                  |
        | #/002/tests/003/data | true  | ignores arrays                                                                   |
        | #/002/tests/004/data | true  | ignores booleans                                                                 |
        | #/002/tests/005/data | true  | ignores null                                                                     |
        #| #/002/tests/006/data | true  | invalid regex string is only an annotation by default                            |

Scenario Outline: validation of IP addresses
/* Schema: 
{"format": "ipv4"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | ignores integers                                                                 |
        | #/003/tests/001/data | true  | ignores floats                                                                   |
        | #/003/tests/002/data | true  | ignores objects                                                                  |
        | #/003/tests/003/data | true  | ignores arrays                                                                   |
        | #/003/tests/004/data | true  | ignores booleans                                                                 |
        | #/003/tests/005/data | true  | ignores null                                                                     |
        #| #/003/tests/006/data | true  | invalid ipv4 string is only an annotation by default                             |

Scenario Outline: validation of IPv6 addresses
/* Schema: 
{"format": "ipv6"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | ignores integers                                                                 |
        | #/004/tests/001/data | true  | ignores floats                                                                   |
        | #/004/tests/002/data | true  | ignores objects                                                                  |
        | #/004/tests/003/data | true  | ignores arrays                                                                   |
        | #/004/tests/004/data | true  | ignores booleans                                                                 |
        | #/004/tests/005/data | true  | ignores null                                                                     |
        #| #/004/tests/006/data | true  | invalid ipv6 string is only an annotation by default                             |

Scenario Outline: validation of IDN hostnames
/* Schema: 
{"format": "idn-hostname"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | ignores integers                                                                 |
        | #/005/tests/001/data | true  | ignores floats                                                                   |
        | #/005/tests/002/data | true  | ignores objects                                                                  |
        | #/005/tests/003/data | true  | ignores arrays                                                                   |
        | #/005/tests/004/data | true  | ignores booleans                                                                 |
        | #/005/tests/005/data | true  | ignores null                                                                     |
        #| #/005/tests/006/data | true  | invalid idn-hostname string is only an annotation by default                     |

Scenario Outline: validation of hostnames
/* Schema: 
{"format": "hostname"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | ignores integers                                                                 |
        | #/006/tests/001/data | true  | ignores floats                                                                   |
        | #/006/tests/002/data | true  | ignores objects                                                                  |
        | #/006/tests/003/data | true  | ignores arrays                                                                   |
        | #/006/tests/004/data | true  | ignores booleans                                                                 |
        | #/006/tests/005/data | true  | ignores null                                                                     |
        #| #/006/tests/006/data | true  | invalid hostname string is only an annotation by default                         |

Scenario Outline: validation of date strings
/* Schema: 
{"format": "date"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | ignores integers                                                                 |
        | #/007/tests/001/data | true  | ignores floats                                                                   |
        | #/007/tests/002/data | true  | ignores objects                                                                  |
        | #/007/tests/003/data | true  | ignores arrays                                                                   |
        | #/007/tests/004/data | true  | ignores booleans                                                                 |
        | #/007/tests/005/data | true  | ignores null                                                                     |
        #| #/007/tests/006/data | true  | invalid date string is only an annotation by default                             |

Scenario Outline: validation of date-time strings
/* Schema: 
{"format": "date-time"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | ignores integers                                                                 |
        | #/008/tests/001/data | true  | ignores floats                                                                   |
        | #/008/tests/002/data | true  | ignores objects                                                                  |
        | #/008/tests/003/data | true  | ignores arrays                                                                   |
        | #/008/tests/004/data | true  | ignores booleans                                                                 |
        | #/008/tests/005/data | true  | ignores null                                                                     |
        #| #/008/tests/006/data | true  | invalid date-time string is only an annotation by default                        |

Scenario Outline: validation of time strings
/* Schema: 
{"format": "time"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | ignores integers                                                                 |
        | #/009/tests/001/data | true  | ignores floats                                                                   |
        | #/009/tests/002/data | true  | ignores objects                                                                  |
        | #/009/tests/003/data | true  | ignores arrays                                                                   |
        | #/009/tests/004/data | true  | ignores booleans                                                                 |
        | #/009/tests/005/data | true  | ignores null                                                                     |
        #| #/009/tests/006/data | true  | invalid time string is only an annotation by default                             |

Scenario Outline: validation of JSON pointers
/* Schema: 
{"format": "json-pointer"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | true  | ignores integers                                                                 |
        | #/010/tests/001/data | true  | ignores floats                                                                   |
        | #/010/tests/002/data | true  | ignores objects                                                                  |
        | #/010/tests/003/data | true  | ignores arrays                                                                   |
        | #/010/tests/004/data | true  | ignores booleans                                                                 |
        | #/010/tests/005/data | true  | ignores null                                                                     |
        #| #/010/tests/006/data | true  | invalid json-pointer string is only an annotation by default                     |

Scenario Outline: validation of relative JSON pointers
/* Schema: 
{"format": "relative-json-pointer"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | true  | ignores integers                                                                 |
        | #/011/tests/001/data | true  | ignores floats                                                                   |
        | #/011/tests/002/data | true  | ignores objects                                                                  |
        | #/011/tests/003/data | true  | ignores arrays                                                                   |
        | #/011/tests/004/data | true  | ignores booleans                                                                 |
        | #/011/tests/005/data | true  | ignores null                                                                     |
        #| #/011/tests/006/data | true  | invalid relative-json-pointer string is only an annotation by default            |

Scenario Outline: validation of IRIs
/* Schema: 
{"format": "iri"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | true  | ignores integers                                                                 |
        | #/012/tests/001/data | true  | ignores floats                                                                   |
        | #/012/tests/002/data | true  | ignores objects                                                                  |
        | #/012/tests/003/data | true  | ignores arrays                                                                   |
        | #/012/tests/004/data | true  | ignores booleans                                                                 |
        | #/012/tests/005/data | true  | ignores null                                                                     |
        #| #/012/tests/006/data | true  | invalid iri string is only an annotation by default                              |

Scenario Outline: validation of IRI references
/* Schema: 
{"format": "iri-reference"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/013/tests/000/data | true  | ignores integers                                                                 |
        | #/013/tests/001/data | true  | ignores floats                                                                   |
        | #/013/tests/002/data | true  | ignores objects                                                                  |
        | #/013/tests/003/data | true  | ignores arrays                                                                   |
        | #/013/tests/004/data | true  | ignores booleans                                                                 |
        | #/013/tests/005/data | true  | ignores null                                                                     |
        #| #/013/tests/006/data | true  | invalid iri-reference string is only an annotation by default                    |

Scenario Outline: validation of URIs
/* Schema: 
{"format": "uri"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/014/tests/000/data | true  | ignores integers                                                                 |
        | #/014/tests/001/data | true  | ignores floats                                                                   |
        | #/014/tests/002/data | true  | ignores objects                                                                  |
        | #/014/tests/003/data | true  | ignores arrays                                                                   |
        | #/014/tests/004/data | true  | ignores booleans                                                                 |
        | #/014/tests/005/data | true  | ignores null                                                                     |
        #| #/014/tests/006/data | true  | invalid uri string is only an annotation by default                              |

Scenario Outline: validation of URI references
/* Schema: 
{"format": "uri-reference"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/015/tests/000/data | true  | ignores integers                                                                 |
        | #/015/tests/001/data | true  | ignores floats                                                                   |
        | #/015/tests/002/data | true  | ignores objects                                                                  |
        | #/015/tests/003/data | true  | ignores arrays                                                                   |
        | #/015/tests/004/data | true  | ignores booleans                                                                 |
        | #/015/tests/005/data | true  | ignores null                                                                     |
        #| #/015/tests/006/data | true  | invalid uri-reference string is only an annotation by default                    |

Scenario Outline: validation of URI templates
/* Schema: 
{"format": "uri-template"}
*/
    Given the input JSON file "format.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/016/tests/000/data | true  | ignores integers                                                                 |
        | #/016/tests/001/data | true  | ignores floats                                                                   |
        | #/016/tests/002/data | true  | ignores objects                                                                  |
        | #/016/tests/003/data | true  | ignores arrays                                                                   |
        | #/016/tests/004/data | true  | ignores booleans                                                                 |
        | #/016/tests/005/data | true  | ignores null                                                                     |
        #| #/016/tests/006/data | true  | invalid uri-template string is only an annotation by default                     |

Scenario Outline: validation of UUIDs
/* Schema: 
{ "format": "uuid" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/017/tests/000/data | true  | ignores integers                                                                 |
        | #/017/tests/001/data | true  | ignores floats                                                                   |
        | #/017/tests/002/data | true  | ignores objects                                                                  |
        | #/017/tests/003/data | true  | ignores arrays                                                                   |
        | #/017/tests/004/data | true  | ignores booleans                                                                 |
        | #/017/tests/005/data | true  | ignores null                                                                     |
        #| #/017/tests/006/data | true  | invalid uuid string is only an annotation by default                             |

Scenario Outline: validation of durations
/* Schema: 
{ "format": "duration" }
*/
    Given the input JSON file "format.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/018/tests/000/data | true  | ignores integers                                                                 |
        | #/018/tests/001/data | true  | ignores floats                                                                   |
        | #/018/tests/002/data | true  | ignores objects                                                                  |
        | #/018/tests/003/data | true  | ignores arrays                                                                   |
        | #/018/tests/004/data | true  | ignores booleans                                                                 |
        | #/018/tests/005/data | true  | ignores null                                                                     |
        #| #/018/tests/006/data | true  | invalid duration string is only an annotation by default                         |
