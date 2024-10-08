Feature: Truncate file names

Scenario Outline: Validate path and filename truncation for valid paths
	Given a path "<inputPath>"
	When the path is truncated to the maximum length of <maxLength>
	Then the result should be "<expectedPath>"

Examples:
	| inputPath                           | maxLength | expectedPath                          |
	| foo/bar/my.file.cs                  | 18        | foo/bar/my.file.cs                  |
	| foo/bar/my.file.cs                  | 17        | foo/bar/_._ile.cs                   |
	| foo/bar/my.file.cs                  | 16        | foo/bar/_._le.cs                    |
	| foo/bar/my.file.cs                  | 15        | foo/bar/_._e.cs                     |
	| foo/bar/my.longer.file.cs           | 25        | foo/bar/my.longer.file.cs           |
	| foo/bar/my.longer.file.cs           | 24        | foo/bar/my_._ger.file.cs            |
	| foo/bar/my.longer.file.cs           | 23        | foo/bar/my_._er.file.cs             |
	| foo/bar/my.longer.file.cs           | 22        | foo/bar/my_._r.file.cs              |
	| foo/bar/my.longer.file.cs           | 21        | foo/bar/my_._file.cs                |
	| foo/bar/my.longer.file.cs           | 20        | foo/bar/my_._file.cs                |
	| foo/bar/my.longer.file.cs           | 19        | foo/bar/m_._file.cs                 |
	| foo/bar/my.longer.file.cs           | 18        | foo/bar/_._file.cs                  |
	| foo/bar/my.longer.file.cs           | 17        | foo/bar/_._ile.cs                   |
	| foo/bar/my.longer.file.cs           | 16        | foo/bar/_._le.cs                    |
	| foo/bar/my.longer.file.cs           | 15        | foo/bar/_._e.cs                     |
	| foo/bar/my.extremely.longer.file.cs | 35        | foo/bar/my.extremely.longer.file.cs |
	| foo/bar/my.extremely.longer.file.cs | 34        | foo/bar/my.extrem_._longer.file.cs  |
	| foo/bar/my.extremely.longer.file.cs | 33        | foo/bar/my.extre_._longer.file.cs   |
	| foo/bar/my.extremely.longer.file.cs | 32        | foo/bar/my.extr_._longer.file.cs    |
	| foo/bar/my.extremely.longer.file.cs | 31        | foo/bar/my.ext_._longer.file.cs     |
	| foo/bar/my.extremely.longer.file.cs | 30        | foo/bar/my.ex_._longer.file.cs      |
	| foo/bar/my.extremely.longer.file.cs | 29        | foo/bar/my.e_._longer.file.cs       |
	| foo/bar/my.extremely.longer.file.cs | 28        | foo/bar/my_._longer.file.cs         |
	| foo/bar/my.extremely.longer.file.cs | 27        | foo/bar/my_._longer.file.cs         |
	| foo/bar/my.extremely.longer.file.cs | 26        | foo/bar/my_._onger.file.cs          |
	| foo/bar/my.extremely.longer.file.cs | 25        | foo/bar/my_._nger.file.cs           |
	| foo/bar/my.extremely.longer.file.cs | 24        | foo/bar/my_._ger.file.cs            |
	| foo/bar/my.extremely.longer.file.cs | 23        | foo/bar/my_._er.file.cs             |
	| foo/bar/my.extremely.longer.file.cs | 22        | foo/bar/my_._r.file.cs              |
	| foo/bar/my.extremely.longer.file.cs | 21        | foo/bar/my_._file.cs                |
	| foo/bar/my.extremely.longer.file.cs | 20        | foo/bar/my_._file.cs                |
	| foo/bar/my.extremely.longer.file.cs | 19        | foo/bar/m_._file.cs                 |
	| foo/bar/m.file                      | 14        | foo/bar/m.file                      |
	| foo/bar/m                           | 9         | foo/bar/m                           |
	| foo/bar/fives.file                  | 18        | foo/bar/fives.file                  |
	| foo/bar/fives.file                  | 17        | foo/bar/_._s.file                   |
