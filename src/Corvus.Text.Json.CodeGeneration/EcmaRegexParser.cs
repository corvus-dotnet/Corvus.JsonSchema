// <copyright file="EcmaRegexParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Text;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>The kinds of node in the syntax tree of an ECMA-262 pattern.</summary>
internal enum EcmaRegexNodeKind
{
    /// <summary>Nothing: an empty alternative.</summary>
    Empty,

    /// <summary>One character.</summary>
    Char,

    /// <summary>One character of a set.</summary>
    Set,

    /// <summary>Terms one after another.</summary>
    Cat,

    /// <summary>Alternatives.</summary>
    Alt,

    /// <summary>A group, capturing or not.</summary>
    Group,

    /// <summary>A quantified term.</summary>
    Repeat,

    /// <summary><c>^</c> outside a group with the m modifier.</summary>
    Bol,

    /// <summary><c>$</c> outside a group with the m modifier.</summary>
    Eol,

    /// <summary><c>^</c> inside a group with the m modifier.</summary>
    LineStart,

    /// <summary><c>$</c> inside a group with the m modifier.</summary>
    LineEnd,

    /// <summary><c>\b</c>.</summary>
    WordBoundary,

    /// <summary><c>\B</c>.</summary>
    NotWordBoundary,

    /// <summary>A lookahead or a lookbehind.</summary>
    Look,

    /// <summary>A backreference.</summary>
    Backref,
}

/// <summary>One element of the syntax tree of an ECMA-262 pattern.</summary>
internal sealed class EcmaRegexNode(EcmaRegexNodeKind kind)
{
    /// <summary>The max of a quantifier with no upper bound.</summary>
    public const int Unbounded = -1;

    private static readonly EcmaRegexNode[] NoNodes = [];

    /// <summary>Gets the kind of the node.</summary>
    public EcmaRegexNodeKind Kind { get; } = kind;

    /// <summary>Gets or sets the character of a Char: a code point with the u flag grammar and a UTF-16 code unit without.</summary>
    public int CodePoint { get; set; }

    /// <summary>Gets or sets the set of a Set.</summary>
    public int[] Set { get; set; } = [];

    /// <summary>
    /// Gets or sets, when not null, another way to write the set of a Set: its members in the order the pattern
    /// gave them, as ranges, all of them in the Basic Multilingual Plane and none a surrogate. The set is these, or
    /// every character but these when <see cref="Negated"/>.
    /// </summary>
    public int[]? Items { get; set; }

    /// <summary>
    /// Gets or sets, when not null, the General_Category values a Set names, each as the tables it is the union
    /// of. The set is these and <see cref="Items"/> together, or every character outside them when <see cref="Negated"/>.
    /// </summary>
    public int[]? Categories { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Items"/> and <see cref="Categories"/> are what the set leaves out.</summary>
    public bool Negated { get; set; }

    /// <summary>Gets or sets the children of Cat and Alt, and the one child of Group, Repeat and Look.</summary>
    public EcmaRegexNode[] Subs { get; set; } = NoNodes;

    /// <summary>Gets or sets the least count of a Repeat.</summary>
    public int Min { get; set; }

    /// <summary>Gets or sets the greatest count of a Repeat, or <see cref="Unbounded"/>.</summary>
    public int Max { get; set; }

    /// <summary>Gets or sets a value indicating whether a Repeat is lazy.</summary>
    public bool Lazy { get; set; }

    /// <summary>Gets or sets the number of the capturing group of a Group, counted from 1 as ECMA-262 does, or 0.</summary>
    public int Group { get; set; }

    /// <summary>
    /// Gets or sets the groups a Backref names. A name can belong to several groups in separate alternatives, of
    /// which at most one has taken part at any time.
    /// </summary>
    public int[] Groups { get; set; } = [];

    /// <summary>Gets or sets the group name of a named Backref until it is resolved.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets a value indicating whether a Backref, WordBoundary or NotWordBoundary is inside a case-insensitive group.</summary>
    public bool Fold { get; set; }

    /// <summary>Gets or sets a value indicating whether a Look is a lookbehind.</summary>
    public bool Behind { get; set; }

    /// <summary>Gets or sets a value indicating whether a Look is negative.</summary>
    public bool Negative { get; set; }

    /// <summary>Gets or sets the number of the last capturing group before the node. The groups inside it are those numbered above it, up to <see cref="LastGroup"/>.</summary>
    public int FirstGroup { get; set; }

    /// <summary>Gets or sets the number of the last capturing group inside the node.</summary>
    public int LastGroup { get; set; }

    /// <summary>Gets or sets the least number of characters the node matches, once measured, and -1 before.</summary>
    public long MinLength { get; set; } = -1;

    /// <summary>Gets or sets the greatest number of characters the node matches, once measured.</summary>
    public long MaxLength { get; set; }
}

/// <summary>The error of a pattern that is not valid ECMA-262.</summary>
internal sealed class EcmaRegexSyntaxException(string reason, int position)
    : Exception(reason + " at offset " + position.ToString(System.Globalization.CultureInfo.InvariantCulture))
{
    /// <summary>Gets what is wrong.</summary>
    public string Reason { get; } = reason;

    /// <summary>Gets where it is, as an offset in the pattern.</summary>
    public int Position { get; } = position;
}

/// <summary>
/// Reads an ECMA-262 pattern by recursive descent into a tree. With <c>unicode</c> it applies the grammar of the u
/// flag and reads the pattern by code point. Without it, it applies the Annex B grammar of a pattern with no flag and
/// reads the pattern by UTF-16 code unit, as such a pattern is matched. The grammar is that of ECMAScript 2025.
/// </summary>
internal sealed class EcmaRegexParser
{
    /// <summary>Groups may nest this deep. It bounds the recursion of the reader and of the writer.</summary>
    public const int MaxNesting = 200;

    /// <summary>The largest bound of a counted quantifier. No string reaches a larger count.</summary>
    public const int MaxCount = int.MaxValue - 1;

    // The classes ECMA-262 defines, as sets. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and the
    // dot is everything but the four line terminators.
    private static readonly int[] Digit = ['0', '9'];
    private static readonly int[] Word = ['0', '9', 'A', 'Z', '_', '_', 'a', 'z'];

    /// <summary>
    /// With the u flag grammar and case-insensitive matching, <c>\w</c> and <c>\b</c> also count the two characters
    /// that fold to an ASCII letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
    /// </summary>
    private static readonly int[] FoldWord = ['0', '9', 'A', 'Z', '_', '_', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A];

    private static readonly int[] Space =
    [
        0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F, 0x205F,
        0x205F, 0x3000, 0x3000, 0xFEFF, 0xFEFF,
    ];

    private static readonly int[] LineTerminators = ['\n', '\n', '\r', '\r', 0x2028, 0x2029];

    // The same classes as members of a class, in the order the translator has always written them.
    private static readonly int[] DigitItems = ['0', '9'];
    private static readonly int[] WordItems = ['a', 'z', 'A', 'Z', '0', '9', '_', '_'];

    private static readonly int[] SpaceItems =
    [
        0x9, 0x9, 0xA, 0xA, 0xB, 0xB, 0xC, 0xC, 0xD, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0xFEFF, 0xFEFF, 0x1680, 0x1680, 0x2000,
        0x200A, 0x2028, 0x2028, 0x2029, 0x2029, 0x202F, 0x202F, 0x205F, 0x205F, 0x3000, 0x3000,
    ];

    private static readonly int[] LineTerminatorItems = ['\n', '\n', '\r', '\r', 0x2028, 0x2028, 0x2029, 0x2029];

    // The sets that are every character but a few, which most patterns hold, worked out once for each grammar.
    private static readonly Complements CodePointComplements = new(EcmaRegexUnicode.Max);
    private static readonly Complements CodeUnitComplements = new(0xFFFF);

    private readonly int[] p;
    private readonly int max;
    private readonly Complements complements;

    /// <summary>For each group name, its groups: the number of each, then the alternatives that enclose it.</summary>
    private readonly Dictionary<string, List<int[]>> names = [];
    private readonly List<EcmaRegexNode> namedRefs = [];
    private int i;
    private int depth;
    private bool hasNames;
    private int groups;

    /// <summary>The alternatives that enclose the current position, innermost last, as disjunction and alternative.</summary>
    private int[] path = new int[16];
    private int pathLength;
    private int disjunctions;

    // The modifiers in force, which only a group such as (?i:...) changes.
    private bool ignoreCase;
    private bool multiline;
    private bool dotAll;

    /// <summary>The ranges a class has gathered so far, in the order the pattern gave them.</summary>
    private int[] pairs = new int[16];
    private int pairCount;

    /// <summary>The set of the class atom <see cref="ClassAtom"/> last read, when it was a class escape, and null otherwise.</summary>
    private int[]? atomSet;

    /// <summary>The members of <see cref="atomSet"/> in the order to write them, when the escape has one.</summary>
    private int[]? atomItems;

    /// <summary>
    /// The General_Category value of the <c>\p</c> or <c>\P</c> escape <see cref="ClassEscape"/> last read, as its
    /// tables, when .NET has a class for it that no surrogate is in, and whether the escape negated it.
    /// </summary>
    private int engineCategory;
    private bool engineCategoryNegated;

    /// <summary>Initializes a new instance of the <see cref="EcmaRegexParser"/> class.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="unicode">Whether to read it with the grammar of the u flag.</param>
    public EcmaRegexParser(ReadOnlySpan<char> pattern, bool unicode)
    {
        this.Unicode = unicode;
        this.max = unicode ? EcmaRegexUnicode.Max : 0xFFFF;
        this.complements = unicode ? CodePointComplements : CodeUnitComplements;
        int[] units = new int[pattern.Length];
        int n = 0;
        for (int k = 0; k < pattern.Length; k++)
        {
            char c = pattern[k];
            if (unicode && char.IsHighSurrogate(c) && k + 1 < pattern.Length && char.IsLowSurrogate(pattern[k + 1]))
            {
                units[n++] = char.ConvertToUtf32(c, pattern[++k]);
            }
            else
            {
                units[n++] = c;
            }
        }

        if (n != units.Length)
        {
            Array.Resize(ref units, n);
        }

        this.p = units;
    }

    /// <summary>Gets a value indicating whether the pattern is read with the grammar of the u flag.</summary>
    public bool Unicode { get; }

    /// <summary>
    /// Gets the number of capturing groups, from a scan of the whole pattern before it is read, as the meaning of
    /// <c>\1</c> and <c>\k</c> depends on groups that may follow them.
    /// </summary>
    public int GroupCount { get; private set; }

    /// <summary>Gets a value indicating whether the pattern holds a backreference.</summary>
    public bool HasBackreferences { get; private set; }

    /// <summary>Gets every character: the code points with the u flag grammar and the UTF-16 code units without.</summary>
    public int[] Any => this.complements.Any;

    /// <summary>Reads the pattern.</summary>
    /// <returns>The root of its tree.</returns>
    /// <exception cref="EcmaRegexSyntaxException">The pattern is not valid in the grammar.</exception>
    public EcmaRegexNode Parse()
    {
        this.ScanGroups();
        EcmaRegexNode root = this.Disjunction();
        if (this.i < this.p.Length)
        {
            throw this.Error(this.p[this.i] == ')' ? "Unmatched ')'" : "Unexpected character");
        }

        foreach (EcmaRegexNode reference in this.namedRefs)
        {
            if (!this.names.TryGetValue(reference.Name!, out List<int[]>? named))
            {
                throw this.Error("Reference to the unknown group name '" + reference.Name + "'");
            }

            int[] numbers = new int[named.Count];
            for (int k = 0; k < numbers.Length; k++)
            {
                numbers[k] = named[k][0];
            }

            reference.Groups = numbers;
        }

        return root;
    }

    private static bool IsDigit(int c) => c >= '0' && c <= '9';

    private static int HexValue(int c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }

        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }

        return -1;
    }

    private static EcmaRegexNode SetNode(int[] set) => new(EcmaRegexNodeKind.Set) { Set = set };

    private EcmaRegexSyntaxException Error(string reason) => new(reason, this.Offset(this.i));

    /// <summary>The offset in the pattern, in UTF-16 code units, of an index of <see cref="p"/>.</summary>
    private int Offset(int index)
    {
        if (!this.Unicode)
        {
            return index;
        }

        int offset = 0;
        for (int k = 0; k < index && k < this.p.Length; k++)
        {
            offset += this.p[k] > 0xFFFF ? 2 : 1;
        }

        return offset;
    }

    /// <summary>Counts the capturing groups of the pattern and notes whether any is named.</summary>
    private void ScanGroups()
    {
        bool inClass = false;
        for (int k = 0; k < this.p.Length; k++)
        {
            int c = this.p[k];
            if (c == '\\')
            {
                k++;
            }
            else if (c == '[')
            {
                inClass = true;
            }
            else if (c == ']')
            {
                inClass = false;
            }
            else if (c == '(' && !inClass)
            {
                if (k + 1 >= this.p.Length || this.p[k + 1] != '?')
                {
                    this.GroupCount++;
                }
                else if (k + 3 < this.p.Length && this.p[k + 2] == '<' && this.p[k + 3] != '=' && this.p[k + 3] != '!')
                {
                    this.GroupCount++;
                    this.hasNames = true;
                }
            }
        }
    }

    private bool More() => this.i < this.p.Length;

    private int Peek() => this.i < this.p.Length ? this.p[this.i] : -1;

    private bool Eat(int c)
    {
        if (this.i < this.p.Length && this.p[this.i] == c)
        {
            this.i++;
            return true;
        }

        return false;
    }

    private EcmaRegexNode Disjunction()
    {
        if (++this.depth > MaxNesting)
        {
            throw this.Error("Groups nested too deeply");
        }

        if ((2 * this.pathLength) + 2 > this.path.Length)
        {
            Array.Resize(ref this.path, this.path.Length * 2);
        }

        int level = this.pathLength++;
        this.path[2 * level] = ++this.disjunctions;
        this.path[(2 * level) + 1] = 0;
        EcmaRegexNode first = this.Alternative();
        EcmaRegexNode result = first;
        if (this.Peek() == '|')
        {
            List<EcmaRegexNode> alternatives = [first];
            while (this.Eat('|'))
            {
                this.path[(2 * level) + 1]++;
                alternatives.Add(this.Alternative());
            }

            result = new EcmaRegexNode(EcmaRegexNodeKind.Alt) { Subs = [.. alternatives] };
        }

        this.pathLength--;
        this.depth--;
        return result;
    }

    private EcmaRegexNode Alternative()
    {
        List<EcmaRegexNode>? terms = null;
        EcmaRegexNode? only = null;
        while (this.More() && this.Peek() != '|' && this.Peek() != ')')
        {
            EcmaRegexNode term = this.Term();
            if (only is null && terms is null)
            {
                only = term;
            }
            else
            {
                terms ??= [only!];
                terms.Add(term);
            }
        }

        if (terms is not null)
        {
            return new EcmaRegexNode(EcmaRegexNodeKind.Cat) { Subs = [.. terms] };
        }

        return only ?? new EcmaRegexNode(EcmaRegexNodeKind.Empty);
    }

    /// <summary>
    /// The node for one literal character. Inside a case-insensitive group that is the set of the characters
    /// equivalent to it.
    /// </summary>
    private EcmaRegexNode CharNode(int c)
    {
        if (this.ignoreCase)
        {
            int[] set = EcmaRegexUnicode.FoldClosure([c, c], this.Unicode);
            if (set.Length != 2 || set[0] != set[1])
            {
                return SetNode(set);
            }
        }

        return new EcmaRegexNode(EcmaRegexNodeKind.Char) { CodePoint = c };
    }

    /// <summary>The set that also holds, inside a case-insensitive group, every character equivalent to a member.</summary>
    private int[] Folded(int[] set) => this.ignoreCase ? EcmaRegexUnicode.FoldClosure(set, this.Unicode) : set;

    private EcmaRegexNode Term()
    {
        int c = this.Peek();
        int groupsBefore = this.groups;
        int at = this.i;
        bool quantifiable = true;
        EcmaRegexNode atom;
        switch (c)
        {
            case '^':
                this.i++;
                atom = new EcmaRegexNode(this.multiline ? EcmaRegexNodeKind.LineStart : EcmaRegexNodeKind.Bol);
                quantifiable = false;
                break;
            case '$':
                this.i++;
                atom = new EcmaRegexNode(this.multiline ? EcmaRegexNodeKind.LineEnd : EcmaRegexNodeKind.Eol);
                quantifiable = false;
                break;
            case '(':
                atom = this.GroupOrLook();

                // Annex B lets a quantifier follow a lookahead. The u flag does not, and neither lets one follow a
                // lookbehind.
                quantifiable = atom.Kind != EcmaRegexNodeKind.Look || (!this.Unicode && !atom.Behind);
                break;
            case '.':
                this.i++;
                if (this.dotAll)
                {
                    atom = SetNode(this.Any);
                    atom.Items = [];
                }
                else
                {
                    atom = SetNode(this.complements.Dot);
                    atom.Items = LineTerminatorItems;
                }

                atom.Negated = true;
                break;
            case '[':
                atom = this.CharacterClass();
                break;
            case '\\':
                atom = this.AtomEscape();
                quantifiable = atom.Kind != EcmaRegexNodeKind.WordBoundary && atom.Kind != EcmaRegexNodeKind.NotWordBoundary;
                break;
            case '*':
            case '+':
            case '?':
                throw this.Error("Nothing to repeat");
            case '{':
                if (this.Unicode || this.LooksLikeQuantifier())
                {
                    throw this.Error(this.Unicode ? "Lone quantifier bracket" : "Nothing to repeat");
                }

                this.i++;
                atom = this.CharNode('{');
                break;
            case ']':
            case '}':
                if (this.Unicode)
                {
                    throw this.Error("Lone quantifier bracket");
                }

                this.i++;
                atom = this.CharNode(c);
                break;
            default:
                this.i++;
                atom = this.CharNode(c);
                break;
        }

        atom.FirstGroup = groupsBefore;
        atom.LastGroup = this.groups;
        EcmaRegexNode? repeat = this.Quantifier();
        if (repeat is null)
        {
            return atom;
        }

        if (!quantifiable)
        {
            this.i = at;
            throw this.Error("Nothing to repeat");
        }

        repeat.Subs = [atom];
        repeat.FirstGroup = groupsBefore;
        repeat.LastGroup = this.groups;
        return repeat;
    }

    /// <summary>Whether the text at the current '{' reads as <c>{n}</c>, <c>{n,}</c> or <c>{n,m}</c>.</summary>
    private bool LooksLikeQuantifier()
    {
        int k = this.i + 1;
        int start = k;
        while (k < this.p.Length && IsDigit(this.p[k]))
        {
            k++;
        }

        if (k == start || k >= this.p.Length)
        {
            return false;
        }

        if (this.p[k] == '}')
        {
            return true;
        }

        if (this.p[k] != ',')
        {
            return false;
        }

        k++;
        while (k < this.p.Length && IsDigit(this.p[k]))
        {
            k++;
        }

        return k < this.p.Length && this.p[k] == '}';
    }

    /// <summary>Reads a run of decimal digits, which saturates at <see cref="MaxCount"/>.</summary>
    private int Number()
    {
        int start = this.i;
        long v = 0;
        while (this.i < this.p.Length && IsDigit(this.p[this.i]))
        {
            v = Math.Min((v * 10) + (this.p[this.i] - '0'), MaxCount);
            this.i++;
        }

        if (start == this.i)
        {
            throw this.Error("Expected a number");
        }

        return (int)v;
    }

    /// <summary>Reads a quantifier as a Repeat with no child yet, or returns null when there is none.</summary>
    private EcmaRegexNode? Quantifier()
    {
        int c = this.Peek();
        EcmaRegexNode repeat = new(EcmaRegexNodeKind.Repeat);
        if (c == '*')
        {
            this.i++;
            repeat.Max = EcmaRegexNode.Unbounded;
        }
        else if (c == '+')
        {
            this.i++;
            repeat.Min = 1;
            repeat.Max = EcmaRegexNode.Unbounded;
        }
        else if (c == '?')
        {
            this.i++;
            repeat.Max = 1;
        }
        else if (c == '{' && this.LooksLikeQuantifier())
        {
            this.i++;
            repeat.Min = this.Number();
            repeat.Max = repeat.Min;
            if (this.Eat(','))
            {
                repeat.Max = this.Peek() == '}' ? EcmaRegexNode.Unbounded : this.Number();
            }

            if (!this.Eat('}'))
            {
                throw this.Error("Incomplete quantifier");
            }

            if (repeat.Max != EcmaRegexNode.Unbounded && repeat.Max < repeat.Min)
            {
                throw this.Error("Numbers out of order in {} quantifier");
            }
        }
        else
        {
            return null;
        }

        repeat.Lazy = this.Eat('?');
        return repeat;
    }

    /// <summary>Reads a group or a lookaround.</summary>
    private EcmaRegexNode GroupOrLook()
    {
        this.i++;
        if (!this.Eat('?'))
        {
            return this.Capture(null);
        }

        if (this.Eat(':'))
        {
            EcmaRegexNode group = new(EcmaRegexNodeKind.Group) { Subs = [this.Disjunction()] };
            this.Close();
            return group;
        }

        int c = this.Peek();
        if (c == 'i' || c == 'm' || c == 's' || c == '-')
        {
            return this.ModifierGroup();
        }

        if (this.Eat('=') || this.Eat('!'))
        {
            EcmaRegexNode look = new(EcmaRegexNodeKind.Look) { Negative = this.p[this.i - 1] == '!' };
            look.Subs = [this.Disjunction()];
            this.Close();
            return look;
        }

        if (this.Eat('<'))
        {
            if (this.Eat('=') || this.Eat('!'))
            {
                EcmaRegexNode look = new(EcmaRegexNodeKind.Look) { Behind = true, Negative = this.p[this.i - 1] == '!' };
                look.Subs = [this.Disjunction()];
                this.Close();
                return look;
            }

            string name = this.GroupName();

            // Two groups may share a name only when they are in separate alternatives of one disjunction, so that
            // no match can go through both.
            if (this.names.TryGetValue(name, out List<int[]>? others))
            {
                foreach (int[] other in others)
                {
                    if (!this.SeparateAlternatives(other))
                    {
                        throw this.Error("Duplicate group name '" + name + "'");
                    }
                }
            }

            return this.Capture(name);
        }

        throw this.Error("Invalid group");
    }

    /// <summary>
    /// Whether a named group (its number, then the alternatives that enclose it) and the current position lie in
    /// different alternatives of some disjunction.
    /// </summary>
    private bool SeparateAlternatives(int[] other)
    {
        for (int k = 0; (2 * k) + 2 < other.Length && k < this.pathLength && other[(2 * k) + 1] == this.path[2 * k]; k++)
        {
            if (other[(2 * k) + 2] != this.path[(2 * k) + 1])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a group that turns modifiers on or off for its body, such as <c>(?i:...)</c> or <c>(?s-i:...)</c>.
    /// The position is after the "(?".
    /// </summary>
    private EcmaRegexNode ModifierGroup()
    {
        bool savedIgnoreCase = this.ignoreCase;
        bool savedMultiline = this.multiline;
        bool savedDotAll = this.dotAll;
        int seen = 0;
        bool removing = false;
        while (!this.Eat(':'))
        {
            int c = this.Peek();
            if (c == '-')
            {
                if (removing)
                {
                    throw this.Error("Invalid group modifiers");
                }

                this.i++;
                removing = true;
                continue;
            }

            int flag = c == 'i' ? 1 : c == 'm' ? 2 : c == 's' ? 4 : 0;
            if (flag == 0)
            {
                throw this.Error("Invalid group modifiers");
            }

            if ((seen & flag) != 0)
            {
                throw this.Error("Repeated group modifier");
            }

            this.i++;
            seen |= flag;
            if (flag == 1)
            {
                this.ignoreCase = !removing;
            }
            else if (flag == 2)
            {
                this.multiline = !removing;
            }
            else
            {
                this.dotAll = !removing;
            }
        }

        if (seen == 0)
        {
            this.i--;
            throw this.Error("Invalid group modifiers");
        }

        EcmaRegexNode group = new(EcmaRegexNodeKind.Group) { Subs = [this.Disjunction()] };
        this.Close();
        this.ignoreCase = savedIgnoreCase;
        this.multiline = savedMultiline;
        this.dotAll = savedDotAll;
        return group;
    }

    private EcmaRegexNode Capture(string? name)
    {
        EcmaRegexNode group = new(EcmaRegexNodeKind.Group) { Group = ++this.groups };
        if (name is not null)
        {
            int[] named = new int[1 + (2 * this.pathLength)];
            named[0] = group.Group;
            Array.Copy(this.path, 0, named, 1, 2 * this.pathLength);
            if (!this.names.TryGetValue(name, out List<int[]>? list))
            {
                this.names[name] = list = [];
            }

            list.Add(named);
        }

        group.Subs = [this.Disjunction()];
        this.Close();
        return group;
    }

    private void Close()
    {
        if (!this.Eat(')'))
        {
            throw this.Error("Unterminated group");
        }
    }

    /// <summary>
    /// Reads a group name up to and including its '>'. A character of the name may be written as a Unicode escape.
    /// </summary>
    private string GroupName()
    {
        StringBuilder name = new();
        while (!this.Eat('>'))
        {
            if (!this.More())
            {
                throw this.Error("Invalid group name");
            }

            int c = this.p[this.i++];
            if (c == '\\')
            {
                if (!this.Eat('u'))
                {
                    throw this.Error("Invalid group name");
                }

                c = this.UnicodeEscape(braces: true, pairs: true);
                if (c < 0)
                {
                    throw this.Error("Invalid group name");
                }
            }
            else if (!this.Unicode && c >= 0xD800 && c <= 0xDBFF && this.More() && this.p[this.i] >= 0xDC00 && this.p[this.i] <= 0xDFFF)
            {
                // A name is made of code points whatever the grammar.
                c = char.ConvertToUtf32((char)c, (char)this.p[this.i++]);
            }

            if (name.Length == 0 ? !EcmaRegexUnicode.IsNameStart(c) : !EcmaRegexUnicode.IsNamePart(c))
            {
                throw this.Error("Invalid group name");
            }

            if (c > 0xFFFF)
            {
                name.Append(char.ConvertFromUtf32(c));
            }
            else
            {
                name.Append((char)c);
            }
        }

        if (name.Length == 0)
        {
            throw this.Error("Invalid group name");
        }

        return name.ToString();
    }

    /// <summary>Reads an escape outside a class.</summary>
    private EcmaRegexNode AtomEscape()
    {
        this.i++;
        if (!this.More())
        {
            throw this.Error("\\ at end of pattern");
        }

        int c = this.Peek();
        if (c == 'b' || c == 'B')
        {
            this.i++;

            // With the u flag grammar a case-insensitive word boundary counts two more characters as word
            // characters.
            return new EcmaRegexNode(c == 'b' ? EcmaRegexNodeKind.WordBoundary : EcmaRegexNodeKind.NotWordBoundary)
            {
                Fold = this.Unicode && this.ignoreCase,
            };
        }

        if (c == 'k')
        {
            this.i++;

            // Annex B reads \k as the letter k in a pattern with no named group.
            if (!this.Unicode && !this.hasNames)
            {
                return this.CharNode('k');
            }

            if (!this.Eat('<'))
            {
                throw this.Error("Invalid named reference");
            }

            EcmaRegexNode reference = new(EcmaRegexNodeKind.Backref) { Name = this.GroupName(), Fold = this.ignoreCase };
            this.namedRefs.Add(reference);
            this.HasBackreferences = true;
            return reference;
        }

        if (c >= '1' && c <= '9')
        {
            int start = this.i;
            int n = this.Number();
            if (n <= this.GroupCount)
            {
                this.HasBackreferences = true;
                return new EcmaRegexNode(EcmaRegexNodeKind.Backref) { Groups = [n], Fold = this.ignoreCase };
            }

            if (this.Unicode)
            {
                this.i = start;
                throw this.Error("Reference to a group that does not exist");
            }

            // Annex B reads it as an octal escape, or as the digit itself.
            this.i = start;
            return this.CharNode(this.LegacyOctal());
        }

        int[]? set = this.ClassEscape(c);
        if (set is not null)
        {
            EcmaRegexNode node = SetNode(this.Folded(set));
            if (!this.ignoreCase || ReferenceEquals(node.Set, set))
            {
                if (this.engineCategory != 0)
                {
                    node.Categories = [this.engineCategory];
                    node.Items = [];
                    node.Negated = this.engineCategoryNegated;
                }
                else if (this.atomItems is not null)
                {
                    node.Items = this.atomItems;
                    node.Negated = c is 'D' or 'W' or 'S';
                }
            }

            return node;
        }

        return this.CharNode(this.CharacterEscape(inClass: false));
    }

    /// <summary>
    /// Reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one
    /// (8 or 9) stands for itself.
    /// </summary>
    private int LegacyOctal()
    {
        int v = 0;
        int k = 0;
        while (k < 3 && this.More() && this.Peek() >= '0' && this.Peek() <= '7' && (v * 8) + (this.Peek() - '0') <= 255)
        {
            v = (v * 8) + (this.Peek() - '0');
            this.i++;
            k++;
        }

        if (k == 0)
        {
            return this.p[this.i++];
        }

        return v;
    }

    /// <summary>
    /// Reads a class escape such as <c>\d</c> or <c>\p{...}</c> whose letter is <paramref name="c"/>, as a set.
    /// Returns null, reading nothing, when <paramref name="c"/> does not start one.
    /// </summary>
    private int[]? ClassEscape(int c)
    {
        this.engineCategory = 0;
        this.atomItems = null;
        bool foldWord = this.Unicode && this.ignoreCase;
        switch (c)
        {
            case 'd':
                this.i++;
                this.atomItems = DigitItems;
                return Digit;
            case 'D':
                this.i++;
                this.atomItems = DigitItems;
                return this.complements.NotDigit;
            case 'w':
                this.i++;
                this.atomItems = foldWord ? null : WordItems;
                return foldWord ? FoldWord : Word;
            case 'W':
                this.i++;
                this.atomItems = foldWord ? null : WordItems;
                return foldWord ? this.complements.NotFoldWord : this.complements.NotWord;
            case 's':
                this.i++;
                this.atomItems = SpaceItems;
                return Space;
            case 'S':
                this.i++;
                this.atomItems = SpaceItems;
                return this.complements.NotSpace;
            case 'p':
            case 'P':
            {
                // Without the u flag \p is the letter p.
                if (!this.Unicode)
                {
                    return null;
                }

                this.i++;
                if (!this.Eat('{'))
                {
                    throw this.Error("Invalid property name");
                }

                int start = this.i;
                while (this.More() && this.Peek() != '}')
                {
                    this.i++;
                }

                char[] expression = new char[this.i - start];
                for (int k = 0; k < expression.Length; k++)
                {
                    // A property name is ASCII, so a character beyond it only has to stay a character that is in no name.
                    expression[k] = this.p[start + k] > 0xFFFF ? (char)0xFFFF : (char)this.p[start + k];
                }

                if (!this.Eat('}'))
                {
                    throw this.Error("Invalid property name");
                }

                int[]? set = EcmaRegexUnicode.Property(expression, out int categoryTables);
                if (set is null)
                {
                    this.i = start;
                    throw this.Error("Invalid property name");
                }

                // The table of the surrogates is the 28th. A class of .NET that holds them would take half of a
                // character beyond the Basic Multilingual Plane.
                if (categoryTables != 0 && (categoryTables & (1 << 27)) == 0)
                {
                    this.engineCategory = categoryTables;
                    this.engineCategoryNegated = c == 'P';
                }

                return c == 'P' ? EcmaRegexUnicode.Complement(set, this.max) : set;
            }

            default:
                return null;
        }
    }

    /// <summary>Reads exactly <paramref name="digits"/> hexadecimal digits. Returns -1, reading nothing, when they are not there.</summary>
    private int Hex(int digits)
    {
        if (this.i + digits > this.p.Length)
        {
            return -1;
        }

        int v = 0;
        for (int k = 0; k < digits; k++)
        {
            int d = HexValue(this.p[this.i + k]);
            if (d < 0)
            {
                return -1;
            }

            v = (v * 16) + d;
        }

        this.i += digits;
        return v;
    }

    /// <summary>Reads the escape after a backslash, in or out of a class, and returns the character it names.</summary>
    private int CharacterEscape(bool inClass)
    {
        int c = this.p[this.i++];
        switch (c)
        {
            case 'f':
                return '\f';
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'v':
                return 0x0b;
            case 'c':
            {
                int l = this.Peek();
                if ((l >= 'a' && l <= 'z') || (l >= 'A' && l <= 'Z'))
                {
                    this.i++;
                    return l % 32;
                }

                if (!this.Unicode && inClass && (IsDigit(l) || l == '_'))
                {
                    this.i++;
                    return l % 32;
                }

                if (this.Unicode)
                {
                    throw this.Error("Invalid control escape");
                }

                // Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
                this.i--;
                return '\\';
            }

            case '0':
                if (this.More() && IsDigit(this.Peek()))
                {
                    if (this.Unicode)
                    {
                        throw this.Error("Invalid decimal escape");
                    }

                    this.i--;
                    return this.LegacyOctal();
                }

                return 0;
            case 'x':
            {
                int h = this.Hex(2);
                if (h < 0)
                {
                    if (this.Unicode)
                    {
                        throw this.Error("Invalid \\x escape");
                    }

                    return 'x';
                }

                return h;
            }

            case 'u':
            {
                int u = this.UnicodeEscape(braces: this.Unicode, pairs: this.Unicode);
                if (u < 0)
                {
                    if (this.Unicode)
                    {
                        throw this.Error("Invalid \\u escape");
                    }

                    return 'u';
                }

                return u;
            }

            default:
                break;
        }

        if (c >= '1' && c <= '9' && !this.Unicode && inClass)
        {
            this.i--;
            return this.LegacyOctal();
        }

        // Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B
        // allows any character.
        if (this.Unicode && !(c < 128 && "^$\\.*+?()[]{}|/".IndexOf((char)c) >= 0) && !(inClass && c == '-'))
        {
            this.i--;
            throw this.Error("Invalid escape");
        }

        return c;
    }

    /// <summary>
    /// Reads what follows the u of a Unicode escape and returns the character, or -1, reading nothing, when it is
    /// not a well-formed escape. With <paramref name="braces"/> it accepts the form with braces. With
    /// <paramref name="pairs"/> a surrogate pair written as two escapes is one code point.
    /// </summary>
    private int UnicodeEscape(bool braces, bool pairs)
    {
        int start = this.i;
        if (braces && this.Eat('{'))
        {
            int digits = this.i;
            int v = 0;
            while (this.More() && HexValue(this.Peek()) >= 0)
            {
                if (v <= EcmaRegexUnicode.Max)
                {
                    v = (v * 16) + HexValue(this.Peek());
                }

                this.i++;
            }

            if (digits == this.i || !this.Eat('}') || v > EcmaRegexUnicode.Max)
            {
                this.i = start;
                return -1;
            }

            return v;
        }

        int u = this.Hex(4);
        if (u < 0)
        {
            return -1;
        }

        if (pairs && u >= 0xD800 && u <= 0xDBFF && this.i + 6 <= this.p.Length && this.p[this.i] == '\\' && this.p[this.i + 1] == 'u')
        {
            int save = this.i;
            this.i += 2;
            int low = this.Hex(4);
            if (low >= 0xDC00 && low <= 0xDFFF)
            {
                return char.ConvertToUtf32((char)u, (char)low);
            }

            this.i = save;
        }

        return u;
    }

    private void AddPair(int lo, int hi)
    {
        if (this.pairCount + 2 > this.pairs.Length)
        {
            Array.Resize(ref this.pairs, this.pairs.Length * 2);
        }

        this.pairs[this.pairCount++] = lo;
        this.pairs[this.pairCount++] = hi;
    }

    private void AddClassAtom(int c, int[]? set, int[]? items, ref bool ordered)
    {
        if (set is null)
        {
            this.AddPair(c, c);
            return;
        }

        if (items is null || !ordered)
        {
            ordered = false;
            items = set;
        }

        for (int k = 0; k < items.Length; k += 2)
        {
            this.AddPair(items[k], items[k + 1]);
        }
    }

    /// <summary>
    /// Reads a character class from its '[' to its ']'. The General_Category values it names that .NET has a class
    /// for are kept apart from its other members, so that the class can be written with them.
    /// </summary>
    private EcmaRegexNode CharacterClass()
    {
        this.i++;
        bool negated = this.Eat('^');
        this.pairCount = 0;
        List<int>? categories = null;
        int[] categorySet = [];

        // Whether every member so far is as the pattern wrote it. A negated class escape is its whole set.
        bool ordered = true;

        // The members of a negated class escape that is the only thing in the class, such as [\W], which is \W.
        int atoms = 0;
        int[]? onlyNegatedEscape = null;
        int onlyNegatedCategory = 0;
        while (true)
        {
            if (!this.More())
            {
                throw this.Error("Unterminated character class");
            }

            if (this.Eat(']'))
            {
                break;
            }

            int first = this.ClassAtom(out bool firstNegated);
            int[]? firstSet = this.atomSet;
            int[]? firstItems = this.atomItems;
            onlyNegatedEscape = atoms == 0 && firstNegated ? firstItems : null;
            onlyNegatedCategory = atoms++ == 0 && firstNegated && this.engineCategoryNegated ? this.engineCategory : 0;
            int firstCategory = firstSet is not null && !this.engineCategoryNegated ? this.engineCategory : 0;
            if (this.Peek() == '-' && this.i + 1 < this.p.Length && this.p[this.i + 1] != ']')
            {
                int dash = this.i;
                this.i++;
                atoms++;
                onlyNegatedEscape = null;
                onlyNegatedCategory = 0;
                int second = this.ClassAtom(out bool secondNegated);
                int[]? secondSet = this.atomSet;
                if (firstSet is not null || secondSet is not null)
                {
                    if (this.Unicode)
                    {
                        this.i = dash;
                        throw this.Error("Invalid character class range");
                    }

                    // Annex B reads the '-' as itself when a class escape is at either end.
                    ordered &= !firstNegated && !secondNegated;
                    this.AddClassAtom(first, firstSet, firstItems, ref ordered);
                    this.AddPair('-', '-');
                    this.AddClassAtom(second, secondSet, this.atomItems, ref ordered);
                    continue;
                }

                if (second < first)
                {
                    this.i = dash;
                    throw this.Error("Range out of order in character class");
                }

                this.AddPair(first, second);
                continue;
            }

            if (firstCategory != 0 && !this.ignoreCase)
            {
                categories ??= [];
                categories.Add(firstCategory);
                categorySet = EcmaRegexUnicode.Union(categorySet, firstSet!);
                continue;
            }

            ordered &= !firstNegated;
            this.AddClassAtom(first, firstSet, firstItems, ref ordered);
        }

        int[] items = new int[this.pairCount];
        Array.Copy(this.pairs, items, this.pairCount);
        int[] rest = EcmaRegexUnicode.SetOf(this.pairs, this.pairCount);
        int[] all = EcmaRegexUnicode.Union(rest, categorySet);

        // Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a
        // negated class excludes exactly those characters.
        int[] set = this.Folded(all);
        EcmaRegexNode node = SetNode(negated ? EcmaRegexUnicode.Complement(set, this.max) : set);
        bool folded = !ReferenceEquals(set, all);
        if (!folded && atoms == 1 && (onlyNegatedEscape is not null || onlyNegatedCategory != 0))
        {
            node.Items = onlyNegatedEscape ?? [];
            node.Categories = onlyNegatedCategory != 0 ? [onlyNegatedCategory] : null;
            node.Negated = !negated;
        }
        else if (!folded && (rest.Length == 0 || (rest[0] <= 0xFFFF && rest[rest.Length - 1] <= 0xFFFF && !EcmaRegexUnicode.Intersects(rest, [0xD800, 0xDFFF]))))
        {
            // The members can be written as the pattern has them. When a class escape took away that order they are
            // written as their set.
            node.Items = ordered ? items : rest;
            node.Categories = categories?.ToArray();
            node.Negated = negated;
        }

        return node;
    }

    /// <summary>Reads one member of a class. Returns its character, or sets <see cref="atomSet"/> for a class escape.</summary>
    private int ClassAtom(out bool negatedEscape)
    {
        this.atomSet = null;
        this.atomItems = null;
        this.engineCategory = 0;
        negatedEscape = false;
        int c = this.p[this.i++];
        if (c != '\\')
        {
            return c;
        }

        if (!this.More())
        {
            throw this.Error("\\ at end of pattern");
        }

        int e = this.Peek();
        if (e == 'b')
        {
            this.i++;
            return '\b';
        }

        this.atomSet = this.ClassEscape(e);
        if (this.atomSet is not null)
        {
            negatedEscape = e is 'D' or 'W' or 'S' or 'P';
            return 0;
        }

        return this.CharacterEscape(inClass: true);
    }

    /// <summary>The complements of the sets ECMA-262 names, among the characters up to the last one of a grammar.</summary>
    private sealed class Complements(int max)
    {
        public int[] Any { get; } = [0, max];

        public int[] Dot { get; } = EcmaRegexUnicode.Complement(LineTerminators, max);

        public int[] NotDigit { get; } = EcmaRegexUnicode.Complement(Digit, max);

        public int[] NotWord { get; } = EcmaRegexUnicode.Complement(Word, max);

        public int[] NotFoldWord { get; } = EcmaRegexUnicode.Complement(FoldWord, max);

        public int[] NotSpace { get; } = EcmaRegexUnicode.Complement(Space, max);
    }
}