@draft2019-09

Feature: format draft2019-09
    In order to use json-schema
    As a developer
    I want to support format in draft2019-09

Scenario Outline: email format
/* Schema: 
{ "format": "email" }
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
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: idn-email format
/* Schema: 
{ "format": "idn-email" }
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
        | #/001/tests/000/data | true  | all string formats ignore integers                                               |
        | #/001/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/001/tests/002/data | true  | all string formats ignore objects                                                |
        | #/001/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/001/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/001/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: regex format
/* Schema: 
{ "format": "regex" }
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
        | #/002/tests/000/data | true  | all string formats ignore integers                                               |
        | #/002/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/002/tests/002/data | true  | all string formats ignore objects                                                |
        | #/002/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/002/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/002/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: ipv4 format
/* Schema: 
{ "format": "ipv4" }
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
        | #/003/tests/000/data | true  | all string formats ignore integers                                               |
        | #/003/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/003/tests/002/data | true  | all string formats ignore objects                                                |
        | #/003/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/003/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/003/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: ipv6 format
/* Schema: 
{ "format": "ipv6" }
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
        | #/004/tests/000/data | true  | all string formats ignore integers                                               |
        | #/004/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/004/tests/002/data | true  | all string formats ignore objects                                                |
        | #/004/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/004/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/004/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: idn-hostname format
/* Schema: 
{ "format": "idn-hostname" }
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
        | #/005/tests/000/data | true  | all string formats ignore integers                                               |
        | #/005/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/005/tests/002/data | true  | all string formats ignore objects                                                |
        | #/005/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/005/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/005/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: hostname format
/* Schema: 
{ "format": "hostname" }
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
        | #/006/tests/000/data | true  | all string formats ignore integers                                               |
        | #/006/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/006/tests/002/data | true  | all string formats ignore objects                                                |
        | #/006/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/006/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/006/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: date format
/* Schema: 
{ "format": "date" }
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
        | #/007/tests/000/data | true  | all string formats ignore integers                                               |
        | #/007/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/007/tests/002/data | true  | all string formats ignore objects                                                |
        | #/007/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/007/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/007/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: date-time format
/* Schema: 
{ "format": "date-time" }
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
        | #/008/tests/000/data | true  | all string formats ignore integers                                               |
        | #/008/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/008/tests/002/data | true  | all string formats ignore objects                                                |
        | #/008/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/008/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/008/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: time format
/* Schema: 
{ "format": "time" }
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
        | #/009/tests/000/data | true  | all string formats ignore integers                                               |
        | #/009/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/009/tests/002/data | true  | all string formats ignore objects                                                |
        | #/009/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/009/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/009/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: json-pointer format
/* Schema: 
{ "format": "json-pointer" }
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
        | #/010/tests/000/data | true  | all string formats ignore integers                                               |
        | #/010/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/010/tests/002/data | true  | all string formats ignore objects                                                |
        | #/010/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/010/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/010/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: relative-json-pointer format
/* Schema: 
{ "format": "relative-json-pointer" }
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
        | #/011/tests/000/data | true  | all string formats ignore integers                                               |
        | #/011/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/011/tests/002/data | true  | all string formats ignore objects                                                |
        | #/011/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/011/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/011/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: iri format
/* Schema: 
{ "format": "iri" }
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
        | #/012/tests/000/data | true  | all string formats ignore integers                                               |
        | #/012/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/012/tests/002/data | true  | all string formats ignore objects                                                |
        | #/012/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/012/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/012/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: iri-reference format
/* Schema: 
{ "format": "iri-reference" }
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
        | #/013/tests/000/data | true  | all string formats ignore integers                                               |
        | #/013/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/013/tests/002/data | true  | all string formats ignore objects                                                |
        | #/013/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/013/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/013/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: uri format
/* Schema: 
{ "format": "uri" }
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
        | #/014/tests/000/data | true  | all string formats ignore integers                                               |
        | #/014/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/014/tests/002/data | true  | all string formats ignore objects                                                |
        | #/014/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/014/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/014/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: uri-reference format
/* Schema: 
{ "format": "uri-reference" }
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
        | #/015/tests/000/data | true  | all string formats ignore integers                                               |
        | #/015/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/015/tests/002/data | true  | all string formats ignore objects                                                |
        | #/015/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/015/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/015/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: uri-template format
/* Schema: 
{ "format": "uri-template" }
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
        | #/016/tests/000/data | true  | all string formats ignore integers                                               |
        | #/016/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/016/tests/002/data | true  | all string formats ignore objects                                                |
        | #/016/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/016/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/016/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: uuid format
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
        | #/017/tests/000/data | true  | all string formats ignore integers                                               |
        | #/017/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/017/tests/002/data | true  | all string formats ignore objects                                                |
        | #/017/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/017/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/017/tests/005/data | true  | all string formats ignore nulls                                                  |

Scenario Outline: duration format
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
        | #/018/tests/000/data | true  | all string formats ignore integers                                               |
        | #/018/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/018/tests/002/data | true  | all string formats ignore objects                                                |
        | #/018/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/018/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/018/tests/005/data | true  | all string formats ignore nulls                                                  |
