
Feature: spec_tests

Scenario: 4.1. add with missing object [8731cad5-1ab2-4aa6-aa6c-d65af749db93]
	Given the document {"q":{"bar":2}}
	And the patch [{"op":"add","path":"/a/b","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.1.  Adding an Object Member [83ccb92b-5c59-41c1-8546-6e3158e27a58]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [2c36501c-eea5-4104-955c-4f172184f7b3]
	Given the document {"foo":["bar","baz"]}
	And the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [767523f9-0ac0-4140-9884-275c41bf909e]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [0b25458f-25b4-4f6b-95c6-fc33b4620d92]
	Given the document {"foo":["bar","qux","baz"]}
	And the patch [{"op":"remove","path":"/foo/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [76e15d2c-9015-4066-8361-0e4a46a7fa17]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"replace","path":"/baz","value":"boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [6e741589-4131-44fc-be48-88e67bf48878]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	And the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [babd6bd2-1620-46cd-8fb1-7efa0b721c0a]
	Given the document {"foo":["all","grass","cows","eat"]}
	And the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [93c764f0-7ab6-479a-b95a-93a8994b78f7]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	And the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [82378487-db52-4e07-a3e0-eaa4794fb37f]
	Given the document {"baz":"qux"}
	And the patch [{"op":"test","path":"/baz","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.10.  Adding a nested Member Object [cf46c9dd-d90f-4a47-b3bb-5da0e1b70418]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [caded454-8479-4292-8338-569c11f1bf57]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [ec74175c-158b-4cad-b77d-22237716b20f]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.14. ~ Escape Ordering [d7a3b2be-7b57-449f-8387-9bd234754dd7]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":10}]
	When I apply the patch to the document
	Then the transformed document should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [39d8055f-743c-4d59-a852-d89d2ea6afc6]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":"10"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.16. Adding an Array Value [efdf8e5b-f7b3-4f55-8030-71498a93ffc8]
	Given the document {"foo":["bar"]}
	And the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar",["abc","def"]]}
