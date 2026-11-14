// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

/// <summary>
/// Optional CMU Pronouncing Dictionary (cmudict.dict or cmudict-0.7b format).
/// <see cref="EnglishG2P"/> consults it only for words missing from misaki's lexicon,
/// which is mostly proper names.
/// </summary>
public class CmuDictionary
{
    private static readonly Regex AlternateSuffix = new(@"\(\d+\)$", RegexOptions.Compiled);
    private static readonly Regex NonWordChars = new(@"[^A-Z'\-.]", RegexOptions.Compiled);

    private readonly Dictionary<string, string[]> _dict = new(StringComparer.Ordinal);

    public CmuDictionary(string? dictFilePath = null)
    {
        if (!string.IsNullOrEmpty(dictFilePath) && File.Exists(dictFilePath))
        {
            LoadFromFile(dictFilePath);
        }
    }

    public int Count => _dict.Count;

    public bool TryLookup(string word, out string[] arpabetTokens)
    {
        return _dict.TryGetValue(CleanWord(word), out arpabetTokens!);
    }

    public void LoadFromFile(string filePath)
    {
        foreach (var rawLine in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith(";;;", StringComparison.Ordinal)) continue;

            // cmudict.dict appends comments such as "# place, danish" to some entries.
            int comment = rawLine.IndexOf('#');
            string line = (comment >= 0 ? rawLine[..comment] : rawLine).Trim();
            if (line.Length == 0) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string word = CleanWord(AlternateSuffix.Replace(parts[0], ""));
            if (word.Length == 0) continue;

            // The first listed pronunciation is the primary one; keep it.
            _dict.TryAdd(word, parts[1..]);
        }
    }

    public void Add(string word, string[] arpabetTokens) => _dict[CleanWord(word)] = arpabetTokens;

    private static string CleanWord(string word)
    {
        return NonWordChars.Replace(word.Trim().ToUpperInvariant(), "");
    }
}
