
Feature: spec_tests

Scenario: 4.1. add with missing object [85ded2e7-5306-4a42-af4c-962e73a12305]
	Given the document {"q":{"bar":2}}
	And the patch [{"op":"add","path":"/a/b","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.1.  Adding an Object Member [b15b7f79-9459-47cc-98a4-2f30bdb037d0]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [c25fefbc-c099-4ee7-91de-4b135d5caabb]
	Given the document {"foo":["bar","baz"]}
	And the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [714d33d6-7aea-445a-8aa9-1749c54554bf]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [65f4e8f0-6f40-4620-9bed-d4267076ae6b]
	Given the document {"foo":["bar","qux","baz"]}
	And the patch [{"op":"remove","path":"/foo/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [454a648e-f271-4023-8bb1-5a377f26a895]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"replace","path":"/baz","value":"boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [94b15166-83f8-4579-9170-5a99f34979d6]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	And the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [cfb79600-a201-40bc-855d-df4580866955]
	Given the document {"foo":["all","grass","cows","eat"]}
	And the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [5203f990-d996-4bfc-b1f8-80f21457ca77]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	And the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [b88ca386-2906-4d4e-b998-f33ded39dacf]
	Given the document {"baz":"qux"}
	And the patch [{"op":"test","path":"/baz","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.10.  Adding a nested Member Object [f1e982e6-251e-410d-a954-c5f1725c2a94]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [ae003d31-7ee2-40e5-b5e7-f3263652baff]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [cbd7d7a7-a1df-490d-8dee-926835bc3649]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.14. ~ Escape Ordering [8d3c0204-e3e9-427a-bd1d-d0310be67cfc]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":10}]
	When I apply the patch to the document
	Then the transformed document should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [72db1559-2e52-4dbf-9d06-8d9a04f97bd6]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":"10"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.16. Adding an Array Value [6a24f4cb-3a95-49ca-b059-3df4e83e0546]
	Given the document {"foo":["bar"]}
	And the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar",["abc","def"]]}
