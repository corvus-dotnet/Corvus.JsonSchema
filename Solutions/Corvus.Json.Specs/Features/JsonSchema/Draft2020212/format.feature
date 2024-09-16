@draft2020-12

Feature: format draft2020-12
    In order to use json-schema
    As a developer
    I want to support format in draft2020-12

Scenario Outline: email format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "email"
        }
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
        # 12
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 2962
        | #/000/tests/006/data | true  | invalid email string is only an annotation by default                            |

Scenario Outline: idn-email format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "idn-email"
        }
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
        # 12
        | #/001/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/001/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/001/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/001/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/001/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/001/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 2962
        | #/001/tests/006/data | true  | invalid idn-email string is only an annotation by default                        |

Scenario Outline: regex format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "regex"
        }
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
        # 12
        | #/002/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/002/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/002/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/002/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/002/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/002/tests/005/data | true  | all string formats ignore nulls                                                  |
        # ^(abc]
        | #/002/tests/006/data | true  | invalid regex string is only an annotation by default                            |

Scenario Outline: ipv4 format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "ipv4"
        }
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
        # 12
        | #/003/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/003/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/003/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/003/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/003/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/003/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 127.0.0.0.1
        | #/003/tests/006/data | true  | invalid ipv4 string is only an annotation by default                             |

Scenario Outline: ipv6 format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "ipv6"
        }
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
        # 12
        | #/004/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/004/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/004/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/004/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/004/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/004/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 12345::
        | #/004/tests/006/data | true  | invalid ipv6 string is only an annotation by default                             |

Scenario Outline: idn-hostname format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "idn-hostname"
        }
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
        # 12
        | #/005/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/005/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/005/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/005/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/005/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/005/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 〮실례.테스트
        | #/005/tests/006/data | true  | invalid idn-hostname string is only an annotation by default                     |

Scenario Outline: hostname format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "hostname"
        }
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
        # 12
        | #/006/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/006/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/006/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/006/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/006/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/006/tests/005/data | true  | all string formats ignore nulls                                                  |
        # -a-host-name-that-starts-with--
        | #/006/tests/006/data | true  | invalid hostname string is only an annotation by default                         |

Scenario Outline: date format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "date"
        }
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
        # 12
        | #/007/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/007/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/007/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/007/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/007/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/007/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 06/19/1963
        | #/007/tests/006/data | true  | invalid date string is only an annotation by default                             |

Scenario Outline: date-time format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "date-time"
        }
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
        # 12
        | #/008/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/008/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/008/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/008/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/008/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/008/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 1990-02-31T15:59:60.123-08:00
        | #/008/tests/006/data | true  | invalid date-time string is only an annotation by default                        |

Scenario Outline: time format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "time"
        }
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
        # 12
        | #/009/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/009/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/009/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/009/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/009/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/009/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 08:30:06 PST
        | #/009/tests/006/data | true  | invalid time string is only an annotation by default                             |

Scenario Outline: json-pointer format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "json-pointer"
        }
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
        # 12
        | #/010/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/010/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/010/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/010/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/010/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/010/tests/005/data | true  | all string formats ignore nulls                                                  |
        # /foo/bar~
        | #/010/tests/006/data | true  | invalid json-pointer string is only an annotation by default                     |

Scenario Outline: relative-json-pointer format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "relative-json-pointer"
        }
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
        # 12
        | #/011/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/011/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/011/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/011/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/011/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/011/tests/005/data | true  | all string formats ignore nulls                                                  |
        # /foo/bar
        | #/011/tests/006/data | true  | invalid relative-json-pointer string is only an annotation by default            |

Scenario Outline: iri format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "iri"
        }
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
        # 12
        | #/012/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/012/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/012/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/012/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/012/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/012/tests/005/data | true  | all string formats ignore nulls                                                  |
        # http://2001:0db8:85a3:0000:0000:8a2e:0370:7334
        | #/012/tests/006/data | true  | invalid iri string is only an annotation by default                              |

Scenario Outline: iri-reference format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "iri-reference"
        }
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
        # 12
        | #/013/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/013/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/013/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/013/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/013/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/013/tests/005/data | true  | all string formats ignore nulls                                                  |
        # \\WINDOWS\filëßåré
        | #/013/tests/006/data | true  | invalid iri-reference string is only an annotation by default                    |

Scenario Outline: uri format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "uri"
        }
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
        # 12
        | #/014/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/014/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/014/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/014/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/014/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/014/tests/005/data | true  | all string formats ignore nulls                                                  |
        # //foo.bar/?baz=qux#quux
        | #/014/tests/006/data | true  | invalid uri string is only an annotation by default                              |

Scenario Outline: uri-reference format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "uri-reference"
        }
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
        # 12
        | #/015/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/015/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/015/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/015/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/015/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/015/tests/005/data | true  | all string formats ignore nulls                                                  |
        # \\WINDOWS\fileshare
        | #/015/tests/006/data | true  | invalid uri-reference string is only an annotation by default                    |

Scenario Outline: uri-template format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "uri-template"
        }
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
        # 12
        | #/016/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/016/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/016/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/016/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/016/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/016/tests/005/data | true  | all string formats ignore nulls                                                  |
        # http://example.com/dictionary/{term:1}/{term
        | #/016/tests/006/data | true  | invalid uri-template string is only an annotation by default                     |

Scenario Outline: uuid format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "uuid"
        }
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
        # 12
        | #/017/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/017/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/017/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/017/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/017/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/017/tests/005/data | true  | all string formats ignore nulls                                                  |
        # 2eb8aa08-aa98-11ea-b4aa-73b441d1638
        | #/017/tests/006/data | true  | invalid uuid string is only an annotation by default                             |

Scenario Outline: duration format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "duration"
        }
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
        # 12
        | #/018/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/018/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/018/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/018/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/018/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/018/tests/005/data | true  | all string formats ignore nulls                                                  |
        # PT1D
        | #/018/tests/006/data | true  | invalid duration string is only an annotation by default                         |
