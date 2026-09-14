// <copyright file="VocabularyKeywordIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A per-vocabulary index of keywords by name, so that the keywords present in a schema can be
/// found with one pass over the schema's properties instead of one property scan per keyword.
/// </summary>
/// <remarks>
/// The index materialises <see cref="IVocabulary.Keywords"/> once per vocabulary instance and
/// preserves its order, so the lists it produces are the same as
/// <c>vocabulary.Keywords.Where(k =&gt; schema.HasKeyword(k))</c>.
/// </remarks>
internal sealed class VocabularyKeywordIndex
{
    private static readonly ConditionalWeakTable<IVocabulary, VocabularyKeywordIndex> Indexes = new();
    private static readonly ConditionalWeakTable<IVocabulary, VocabularyKeywordIndex>.CreateValueCallback CreateIndex = v => new VocabularyKeywordIndex(v);

    private readonly IKeyword[] keywords;
    private readonly Dictionary<string, int> firstIndexByName = new(StringComparer.Ordinal);
    private readonly int[] nextIndexWithSameName;

    private VocabularyKeywordIndex(IVocabulary vocabulary)
    {
        this.keywords = [.. vocabulary.Keywords];
        this.nextIndexWithSameName = new int[this.keywords.Length];

        // Several keyword objects may share a name (e.g. the format annotation and assertion
        // keywords); chain them so that each name's chain runs in vocabulary order.
        for (int i = this.keywords.Length - 1; i >= 0; i--)
        {
            string name = Encoding.UTF8.GetString(this.keywords[i].KeywordUtf8.ToArray());
            this.nextIndexWithSameName[i] = this.firstIndexByName.TryGetValue(name, out int next) ? next : -1;
            this.firstIndexByName[name] = i;
        }
    }

    /// <summary>
    /// Gets the keywords of the vocabulary, in vocabulary order.
    /// </summary>
    public IReadOnlyList<IKeyword> Keywords => this.keywords;

    /// <summary>
    /// Gets the index for a vocabulary.
    /// </summary>
    /// <param name="vocabulary">The vocabulary.</param>
    /// <returns>The index for the vocabulary.</returns>
    public static VocabularyKeywordIndex For(IVocabulary vocabulary)
    {
        return Indexes.GetValue(vocabulary, CreateIndex);
    }

    /// <summary>
    /// Gets the keywords of the vocabulary that are present in the schema, in vocabulary order.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The keywords present in the schema.</returns>
    public List<IKeyword> GetPresentKeywords(in JsonElement schema)
    {
        List<IKeyword> result = [];

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        int count = this.keywords.Length;
        Span<bool> present = count <= 256 ? stackalloc bool[count] : new bool[count];
        present.Clear();

        foreach (JsonProperty property in schema.EnumerateObject())
        {
            if (this.firstIndexByName.TryGetValue(property.Name, out int index))
            {
                do
                {
                    present[index] = true;
                    index = this.nextIndexWithSameName[index];
                }
                while (index >= 0);
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (present[i])
            {
                result.Add(this.keywords[i]);
            }
        }

        return result;
    }
}