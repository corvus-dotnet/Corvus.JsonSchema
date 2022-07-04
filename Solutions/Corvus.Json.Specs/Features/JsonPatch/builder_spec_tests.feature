
Feature: builder_spec_tests

Scenario: 4.1. add with missing object [3b133eeb-459c-43c7-8f96-e2e1ea05eaf0]
	Given the document {"q":{"bar":2}}
	When I build the patch [{"op":"add","path":"/a/b","value":1}]
	Then a patch exception should be thrown

Scenario: A.1.  Adding an Object Member [de59cea0-506d-43f3-8f89-84e463000e52]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux"}]
	Then the patch result should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [f4fe142f-6170-4b20-af64-12a74db42096]
	Given the document {"foo":["bar","baz"]}
	When I build the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	Then the patch result should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [365546a4-102c-45e7-9c98-92e45d8f497f]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then the patch result should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [c08868e6-5f29-4f16-b717-a09b7253dbd2]
	Given the document {"foo":["bar","qux","baz"]}
	When I build the patch [{"op":"remove","path":"/foo/1"}]
	Then the patch result should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [e2063b3d-1f13-41cc-bd9f-eadbea878e15]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"replace","path":"/baz","value":"boo"}]
	Then the patch result should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [58f84507-54c1-4334-b7d5-3d420a87a266]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	When I build the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	Then the patch result should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [449c2d3b-6a2e-4ae3-9b9a-68b540f7a9e9]
	Given the document {"foo":["all","grass","cows","eat"]}
	When I build the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	Then the patch result should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [c8aac1cd-758e-4924-86d5-5c8477cfe6e9]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	When I build the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	Then the patch result should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [0348511f-b560-48eb-b6cd-67fe0cfc8580]
	Given the document {"baz":"qux"}
	When I build the patch [{"op":"test","path":"/baz","value":"bar"}]
	Then a patch exception should be thrown

Scenario: A.10.  Adding a nested Member Object [9152376e-810e-460e-b080-e304d9644f3f]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	Then the patch result should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [8dbf4d48-6959-47a2-b385-24cfb7915cda]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	Then the patch result should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [07407453-468e-4276-8d50-0ab8b998d8c3]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	Then a patch exception should be thrown

Scenario: A.14. ~ Escape Ordering [29c585d3-5ad6-4cf8-8b4b-7f3ec918be40]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":10}]
	Then the patch result should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [f0ac35a5-e6a0-4144-8a61-c9d2b3645d7e]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":"10"}]
	Then a patch exception should be thrown

Scenario: A.16. Adding an Array Value [37ca57a7-0e6c-401d-85fc-526730273872]
	Given the document {"foo":["bar"]}
	When I build the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	Then the patch result should equal {"foo":["bar",["abc","def"]]}
