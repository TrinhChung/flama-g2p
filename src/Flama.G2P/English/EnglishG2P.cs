// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Flama.G2P;

namespace Flama.G2P.English;

public enum OovStrategy
{
    SimpleRules,    // Convert letter by letter using rule-based heuristics
    ReturnOriginal, // Return the original word verbatim
    Throw           // Throw an exception when a word is not found
}

public class EnglishG2P : IG2P
{
    private readonly CmuDictionary _dictionary;
    private readonly OovStrategy _oovStrategy;

    public EnglishG2P(CmuDictionary? dictionary = null, OovStrategy oovStrategy = OovStrategy.ReturnOriginal)
    {
        _dictionary = dictionary ?? new CmuDictionary();
        _oovStrategy = oovStrategy;
    }

    public string Phonemize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Text normalization: numbers, currencies, ordinals, decimals, years, symbols
        text = EnglishTextNormalizer.Normalize(text);

        // 2. Tokenize text into words, punctuation, and whitespace
        var matches = Regex.Matches(text, @"[A-Za-z']+|[^\sA-Za-z']+|\s+");
        var tokens = new List<string>(matches.Count);
        foreach (Match m in matches) tokens.Add(m.Value);

        var sb = new StringBuilder();

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];

            if (string.IsNullOrWhiteSpace(token))
            {
                sb.Append(' ');
                continue;
            }

            if (Regex.IsMatch(token, @"^[A-Za-z']+$"))
            {
                // Contextual rules for function words "the" and "to"
                string lower = token.ToLowerInvariant();
                if (lower == "the" || lower == "to")
                {
                    bool nextVowel = LookaheadNextWordIsVowel(tokens, i + 1);
                    if (lower == "the")
                    {
                        sb.Append(nextVowel ? "ði" : "ðə");
                        continue;
                    }
                    if (lower == "to")
                    {
                        sb.Append(nextVowel ? "tʊ" : "tə");
                        continue;
                    }
                }

                if (TryLookupWord(token, out string ipa))
                {
                    sb.Append(ipa);
                }
                else
                {
                    switch (_oovStrategy)
                    {
                        case OovStrategy.SimpleRules:
                            sb.Append(ConvertOovLetters(token));
                            break;
                        case OovStrategy.ReturnOriginal:
                            sb.Append(token);
                            break;
                        case OovStrategy.Throw:
                            throw new KeyNotFoundException($"The word '{token}' was not found in the CMU dictionary.");
                        default:
                            sb.Append(token);
                            break;
                    }
                }
            }
            else
            {
                // Punctuation (preserve . , ! ? ; : — etc.)
                sb.Append(token);
            }
        }

        return CleanPhonemeOutput(sb.ToString());
    }

    public bool TryLookupWord(string word, out string ipa)
    {
        if (_dictionary.TryLookup(word, out string[] arpabet))
        {
            ipa = ArpabetToIpa.ConvertWord(arpabet);
            return true;
        }

        // Try morphological stemming (-s, -ed, -ing)
        if (TryStem(word, out ipa))
        {
            return true;
        }

        // Try all-caps acronym (e.g. USA, AI, FBI, CIA, VIP)
        if (word.Length >= 2 && word.Length <= 5 && word == word.ToUpperInvariant() && Regex.IsMatch(word, @"^[A-Z]+$"))
        {
            ipa = ConvertAcronym(word);
            return true;
        }

        ipa = string.Empty;
        return false;
    }

    private bool LookaheadNextWordIsVowel(List<string> tokens, int startIndex)
    {
        for (int j = startIndex; j < tokens.Count; j++)
        {
            string next = tokens[j];
            if (string.IsNullOrWhiteSpace(next) || !Regex.IsMatch(next, @"^[A-Za-z']+$"))
            {
                continue;
            }

            if (TryLookupWord(next, out string nextIpa))
            {
                return StartsWithVowelSound(nextIpa);
            }

            char firstChar = char.ToLowerInvariant(next[0]);
            return firstChar is 'a' or 'e' or 'i' or 'o' or 'u';
        }

        return false;
    }

    private static readonly HashSet<char> VowelChars = new()
    {
        'æ', 'ɑ', 'ɔ', 'ɛ', 'ɜ', 'ɪ', 'ʊ', 'ʌ', 'i', 'u', 'ə', 'A', 'I', 'O', 'W', 'Y', 'ᵻ'
    };

    private static bool StartsWithVowelSound(string ipa)
    {
        if (string.IsNullOrEmpty(ipa)) return false;
        foreach (char c in ipa)
        {
            if (c == 'ˈ' || c == 'ˌ') continue;
            return VowelChars.Contains(c);
        }
        return false;
    }

    private bool TryStem(string word, out string ipa)
    {
        ipa = string.Empty;
        string lower = word.ToLowerInvariant();

        // 1. Stem -s, -es, -ies, 's
        if (lower.EndsWith("'s") && lower.Length > 2)
        {
            string stem = lower[..^2];
            if (_dictionary.TryLookup(stem, out string[] arpa))
            {
                ipa = ApplySuffixS(ArpabetToIpa.ConvertWord(arpa));
                return true;
            }
        }
        if (lower.EndsWith("ies") && lower.Length > 4)
        {
            string stem = lower[..^3] + "y";
            if (_dictionary.TryLookup(stem, out string[] arpa))
            {
                ipa = ApplySuffixS(ArpabetToIpa.ConvertWord(arpa));
                return true;
            }
        }
        if (lower.EndsWith("es") && lower.Length > 4 && !lower.EndsWith("ses") && !lower.EndsWith("zes"))
        {
            string stem = lower[..^2];
            if (_dictionary.TryLookup(stem, out string[] arpa))
            {
                ipa = ApplySuffixS(ArpabetToIpa.ConvertWord(arpa));
                return true;
            }
        }
        if (lower.EndsWith("s") && lower.Length > 2 && !lower.EndsWith("ss"))
        {
            string stem = lower[..^1];
            if (_dictionary.TryLookup(stem, out string[] arpa))
            {
                ipa = ApplySuffixS(ArpabetToIpa.ConvertWord(arpa));
                return true;
            }
        }

        // 2. Stem -ed
        if (lower.EndsWith("ed") && lower.Length > 4)
        {
            string stem1 = lower[..^2];
            if (_dictionary.TryLookup(stem1, out string[] arpa1))
            {
                ipa = ApplySuffixEd(ArpabetToIpa.ConvertWord(arpa1));
                return true;
            }

            string stem2 = lower[..^1];
            if (_dictionary.TryLookup(stem2, out string[] arpa2))
            {
                ipa = ApplySuffixEd(ArpabetToIpa.ConvertWord(arpa2));
                return true;
            }

            if (lower.Length > 5 && lower[^3] == lower[^4])
            {
                string stem3 = lower[..^3];
                if (_dictionary.TryLookup(stem3, out string[] arpa3))
                {
                    ipa = ApplySuffixEd(ArpabetToIpa.ConvertWord(arpa3));
                    return true;
                }
            }
        }

        // 3. Stem -ing
        if (lower.EndsWith("ing") && lower.Length > 4)
        {
            string stem1 = lower[..^3];
            if (_dictionary.TryLookup(stem1, out string[] arpa1))
            {
                ipa = ArpabetToIpa.ConvertWord(arpa1) + "ɪŋ";
                return true;
            }

            string stem2 = lower[..^3] + "e";
            if (_dictionary.TryLookup(stem2, out string[] arpa2))
            {
                ipa = ArpabetToIpa.ConvertWord(arpa2) + "ɪŋ";
                return true;
            }

            if (lower.Length > 5 && lower[^4] == lower[^5])
            {
                string stem3 = lower[..^4];
                if (_dictionary.TryLookup(stem3, out string[] arpa3))
                {
                    ipa = ArpabetToIpa.ConvertWord(arpa3) + "ɪŋ";
                    return true;
                }
            }
        }

        return false;
    }

    private static string ApplySuffixS(string stemIpa)
    {
        if (string.IsNullOrEmpty(stemIpa)) return "s";

        char last = stemIpa[^1];

        // Sibilants: s, z, ʃ, ʒ, ʧ, ʤ -> ᵻz
        if (last is 's' or 'z' or 'ʃ' or 'ʒ' or 'ʧ' or 'ʤ')
        {
            return stemIpa + "ᵻz";
        }

        // Voiceless consonants: p, t, k, f, θ -> s
        if (last is 'p' or 't' or 'k' or 'f' or 'θ')
        {
            return stemIpa + "s";
        }

        // Voiced consonants and vowels -> z
        return stemIpa + "z";
    }

    private static string ApplySuffixEd(string stemIpa)
    {
        if (string.IsNullOrEmpty(stemIpa)) return "d";

        char last = stemIpa[^1];

        // t or d -> ᵻd
        if (last is 't' or 'd')
        {
            return stemIpa + "ᵻd";
        }

        // Voiceless consonants: p, k, f, θ, ʃ, s, ʧ -> t
        if (last is 'p' or 'k' or 'f' or 'θ' or 'ʃ' or 's' or 'ʧ')
        {
            return stemIpa + "t";
        }

        // Voiced consonants and vowels -> d
        return stemIpa + "d";
    }

    private static readonly Dictionary<char, string> LetterNames = new()
    {
        { 'A', "ˈA" }, { 'B', "bˈi" }, { 'C', "sˈi" }, { 'D', "dˈi" }, { 'E', "ˈi" },
        { 'F', "ˈɛf" }, { 'G', "ʤˈi" }, { 'H', "ˈAʧ" }, { 'I', "ˈI" }, { 'J', "ʤˈA" },
        { 'K', "kˈA" }, { 'L', "ˈɛl" }, { 'M', "ˈɛm" }, { 'N', "ˈɛn" }, { 'O', "ˈO" },
        { 'P', "pˈi" }, { 'Q', "kjˈu" }, { 'R', "ˈɑɹ" }, { 'S', "ˈɛs" }, { 'T', "tˈi" },
        { 'U', "jˈu" }, { 'V', "vˈi" }, { 'W', "dˈʌbᵊljˌu" }, { 'X', "ˈɛks" }, { 'Y', "wˈI" },
        { 'Z', "zˈi" }
    };

    private static string ConvertAcronym(string acronym)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < acronym.Length; i++)
        {
            char c = char.ToUpperInvariant(acronym[i]);
            if (LetterNames.TryGetValue(c, out string? name))
            {
                sb.Append(name);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Fallback method to convert letter-by-letter to IPA using basic digraph and letter mapping.
    /// This is a heuristic fallback and is not linguistically accurate for all English words.
    /// </summary>
    private static string ConvertOovLetters(string word)
    {
        var sb = new StringBuilder();
        string lower = word.ToLowerInvariant();

        for (int i = 0; i < lower.Length; i++)
        {
            char ch = lower[i];
            char next = i + 1 < lower.Length ? lower[i + 1] : '\0';

            // Digraph rules
            if (ch == 't' && next == 'h') { sb.Append("θ"); i++; continue; }
            if (ch == 's' && next == 'h') { sb.Append("ʃ"); i++; continue; }
            if (ch == 'c' && next == 'h') { sb.Append("ʧ"); i++; continue; }
            if (ch == 'p' && next == 'h') { sb.Append("f"); i++; continue; }
            if (ch == 'n' && next == 'g') { sb.Append("ŋ"); i++; continue; }

            switch (ch)
            {
                case 'a': sb.Append("æ"); break;
                case 'b': sb.Append("b"); break;
                case 'c': sb.Append(next == 'e' || next == 'i' || next == 'y' ? "s" : "k"); break;
                case 'd': sb.Append("d"); break;
                case 'e': sb.Append("ɛ"); break;
                case 'f': sb.Append("f"); break;
                case 'g': sb.Append("ɡ"); break;
                case 'h': sb.Append("h"); break;
                case 'i': sb.Append("ɪ"); break;
                case 'j': sb.Append("ʤ"); break;
                case 'k': sb.Append("k"); break;
                case 'l': sb.Append("l"); break;
                case 'm': sb.Append("m"); break;
                case 'n': sb.Append("n"); break;
                case 'o': sb.Append("ɑ"); break;
                case 'p': sb.Append("p"); break;
                case 'q': sb.Append("k"); break;
                case 'r': sb.Append("ɹ"); break;
                case 's': sb.Append("s"); break;
                case 't': sb.Append("t"); break;
                case 'u': sb.Append("ʌ"); break;
                case 'v': sb.Append("v"); break;
                case 'w': sb.Append("w"); break;
                case 'x': sb.Append("ks"); break;
                case 'y': sb.Append("j"); break;
                case 'z': sb.Append("z"); break;
                default: break;
            }
        }

        return sb.ToString();
    }

    private static string CleanPhonemeOutput(string input)
    {
        // Replace multiple spaces with single space
        string cleaned = Regex.Replace(input, @"\s+", " ");
        return cleaned.Trim();
    }
}
