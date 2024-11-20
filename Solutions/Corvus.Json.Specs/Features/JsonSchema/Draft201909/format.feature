@draft2019-09

Feature: format draft2019-09
    In order to use json-schema
    As a developer
    I want to support format in draft2019-09

Scenario Outline: email format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: idn-email format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: regex format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: ipv4 format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: ipv6 format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: idn-hostname format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: hostname format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: date format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: date-time format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: time format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: json-pointer format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: relative-json-pointer format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: iri format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: iri-reference format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: uri format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: uri-reference format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: uri-template format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: uuid format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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

Scenario Outline: duration format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
