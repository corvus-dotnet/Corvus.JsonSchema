Feature: uri-extensions
	Uri extension features

Scenario: Change an existing parameter within multiple
	Given the target uri "http://example/customer?view=false&foo=bar"
	When I get the query string parameters for the target uri
	And I set the parameter called "view" to "true"
	And I make a template for the target uri from the parameters
	Then the resolved template should be one of
		| values                                    |
		| http://example/customer?view=true&foo=bar |
		| http://example/customer?foo=bar&view=true |

Scenario: Change an existing parameter
	Given the target uri "http://example/customer?view=false&foo=bar"
	And I make a template for the target uri
	When I set the template parameter called "view" to the bool true
	Then the resolved template should be one of
		| values                                    |
		| http://example/customer?view=true&foo=bar |
		| http://example/customer?foo=bar&view=true |

Scenario: Clear an existing parameter
	Given the target uri "http://example/customer?view=false&foo=bar"
	And I make a template for the target uri
	When I clear the template parameter called "view"
	Then the resolved template should be one of
		| values                          |
		| http://example/customer?foo=bar |

Scenario: Add multiple parameters
	Given the target uri "http://example/customer"
	When I make a template for the target uri with the parameters
		| name | value |
		| id   | 99    |
		| view | false |
	Then the resolved template should be one of
		| values                                   |
		| http://example/customer?id=99&view=false |
		| http://example/customer?view=false&id=99 |

Scenario: Add multiple parameters as params
	Given the target uri "http://example/customer"
	When I make a template for the target uri with the parameters as params
		| name | value |
		| id   | 99    |
		| view | false |
	Then the resolved template should be one of
		| values                                   |
		| http://example/customer?id=99&view=false |
		| http://example/customer?view=false&id=99 |

Scenario: Add parameters to query string with URI ignoring path parameter
	Given the target uri "http://example/customer/{id}?view=true"
	When I get the query string parameters for the target uri
	And I set the parameter called "context" to "detail"
	And I make a template for the target uri from the parameters
	When I set the template parameter called "id" to the integer 99
	Then the resolved template should be one of
		| values                                              |
		| http://example/customer/99?view=true&context=detail |
		| http://example/customer/99?context=detail&view=true |