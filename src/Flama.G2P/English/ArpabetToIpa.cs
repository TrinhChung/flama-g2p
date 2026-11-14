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

namespace Flama.G2P.English;

/// <summary>
/// Converts CMUdict ARPAbet into misaki-style phonemes (Kokoro's input alphabet).
/// Used for CMUdict-only words and for the neural OOV fallback; words in misaki's own
/// lexicon never go through here. The rules (flap, syllabic l/n, -ed/-es vowel,
/// British non-rhotic r) were tuned against misaki's gold dictionaries.
/// </summary>
public static class ArpabetToIpa
{
    private static readonly Dictionary<string, string> Consonants = new(StringComparer.Ordinal)
    {
        { "B", "b" }, { "D", "d" }, { "F", "f" }, { "G", "ɡ" }, { "HH", "h" },
        { "K", "k" }, { "L", "l" }, { "M", "m" }, { "N", "n" }, { "P", "p" },
        { "R", "ɹ" }, { "S", "s" }, { "T", "t" }, { "V", "v" }, { "W", "w" },
        { "Y", "j" }, { "Z", "z" },
        { "CH", "ʧ" }, { "DH", "ð" }, { "JH", "ʤ" }, { "NG", "ŋ" },
        { "SH", "ʃ" }, { "TH", "θ" }, { "ZH", "ʒ" }
    };

    private static readonly Dictionary<string, string> UsVowels = new(StringComparer.Ordinal)
    {
        { "AA", "ɑ" }, { "AE", "æ" }, { "AO", "ɔ" }, { "AW", "W" }, { "AY", "I" },
        { "EH", "ɛ" }, { "EY", "A" }, { "IY", "i" }, { "OW", "O" }, { "OY", "Y" },
        { "UH", "ʊ" }, { "UW", "u" }
    };

    private static readonly Dictionary<string, string> GbVowels = new(StringComparer.Ordinal)
    {
        { "AA", "ɑː" }, { "AE", "a" }, { "AO", "ɔː" }, { "AW", "W" }, { "AY", "I" },
        { "EH", "ɛ" }, { "EY", "A" }, { "IY", "i" }, { "OW", "Q" }, { "OY", "Y" },
        { "UH", "ʊ" }, { "UW", "u" }
    };

    /// <summary>British vowels before a dropped (non-prevocalic) r.</summary>
    private static readonly Dictionary<string, string> GbRColored = new(StringComparer.Ordinal)
    {
        { "AA", "ɑː" }, { "AO", "ɔː" }, { "EH", "ɛː" }, { "IH", "ɪə" }, { "IY", "ɪə" },
        { "UH", "ʊə" }, { "UW", "ʊə" }, { "AY", "Iə" }, { "AW", "Wə" }
    };

    private readonly record struct Seg(string Base, int Stress)
    {
        public bool IsVowel => Stress >= 0;
    }

    /// <summary>American English (misaki US phoneme set).</summary>
    public static string ConvertWord(string[] arpabetTokens) => ConvertWord(arpabetTokens, british: false);

    public static string ConvertWord(IReadOnlyList<string> arpabetTokens, bool british)
    {
        var segs = Parse(arpabetTokens);
        return british ? ConvertGb(segs) : ConvertUs(segs);
    }

    private static List<Seg> Parse(IReadOnlyList<string> tokens)
    {
        var segs = new List<Seg>(tokens.Count);
        foreach (string raw in tokens)
        {
            string token = raw.Trim().ToUpperInvariant();
            if (token.Length == 0) continue;
            int stress = -1;
            if (char.IsDigit(token[^1]))
            {
                stress = token[^1] - '0';
                token = token[..^1];
            }
            if (Consonants.ContainsKey(token))
            {
                segs.Add(new Seg(token, -1));
            }
            else if (UsVowels.ContainsKey(token) || token is "AH" or "ER" or "IH")
            {
                // CMUdict always marks vowel stress; treat a bare vowel as unstressed.
                segs.Add(new Seg(token, stress < 0 ? 0 : stress));
            }
            // Unknown symbols (comments, junk) are dropped instead of leaking into the output.
        }
        return segs;
    }

    private static string StressMark(int stress) => stress switch
    {
        1 => "ˈ",
        2 => "ˌ",
        _ => ""
    };

    /// <summary>Unstressed AH before l/n that forms its own syllable ("apple", "button").</summary>
    private static bool IsSyllabicSchwa(List<Seg> segs, int i, string nextBase)
    {
        Seg s = segs[i];
        if (s.Base != "AH" || s.Stress != 0) return false;
        if (i == 0 || segs[i - 1].IsVowel) return false;
        if (i + 1 >= segs.Count || segs[i + 1].Base != nextBase) return false;
        return i + 2 >= segs.Count || !segs[i + 2].IsVowel;
    }

    private static string ConvertUs(List<Seg> segs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < segs.Count; i++)
        {
            Seg s = segs[i];
            Seg? next = i + 1 < segs.Count ? segs[i + 1] : null;

            if (!s.IsVowel)
            {
                string c = Consonants[s.Base];
                // American flap: t after a vowel or r, before an unstressed vowel ("water", "city").
                if (s.Base == "T" && sb.Length > 0 && EnglishPhonemes.UsTaus.IndexOf(sb[^1]) >= 0 &&
                    next is { IsVowel: true, Stress: 0 })
                {
                    c = "ɾ";
                }
                sb.Append(c);
                continue;
            }

            string v;
            switch (s.Base)
            {
                case "AH":
                    if (IsSyllabicSchwa(segs, i, "L"))
                    {
                        v = "ᵊ";
                    }
                    else if (s.Stress == 0 && i > 0 && i + 2 == segs.Count && next?.Base == "N" &&
                             segs[i - 1].Base == "T" && sb.Length > 0 && sb[^1] == 'ɾ')
                    {
                        // Final "-ton" after a vowel: glottal t + syllabic n ("button").
                        sb[^1] = 'ʔ';
                        continue;
                    }
                    else
                    {
                        v = s.Stress == 0 ? "ə" : "ʌ";
                    }
                    break;
                case "ER":
                    v = s.Stress == 0 ? "əɹ" : "ɜɹ";
                    break;
                case "IH":
                    // -ed / -es suffix vowel ("wanted", "boxes").
                    v = s.Stress == 0 && i == segs.Count - 2 && next?.Base is "D" or "Z" ? "ᵻ" : "ɪ";
                    break;
                default:
                    v = UsVowels[s.Base];
                    break;
            }
            sb.Append(StressMark(s.Stress)).Append(v);
        }
        return sb.ToString();
    }

    private static string ConvertGb(List<Seg> segs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < segs.Count; i++)
        {
            Seg s = segs[i];
            Seg? next = i + 1 < segs.Count ? segs[i + 1] : null;
            bool nextIsVowel = next is { IsVowel: true };

            if (!s.IsVowel)
            {
                // Non-rhotic: r is only pronounced before a vowel.
                if (s.Base == "R" && !nextIsVowel) continue;
                sb.Append(Consonants[s.Base]);
                continue;
            }

            bool rDropped = next?.Base == "R" && !(i + 2 < segs.Count && segs[i + 2].IsVowel);
            string v;
            switch (s.Base)
            {
                case "AH":
                    v = IsSyllabicSchwa(segs, i, "L") || IsSyllabicSchwa(segs, i, "N")
                        ? "ᵊ"
                        : (s.Stress == 0 ? "ə" : "ʌ");
                    break;
                case "ER":
                    v = (s.Stress == 0 ? "ə" : "ɜː") + (nextIsVowel ? "ɹ" : "");
                    break;
                case "IH":
                    v = rDropped ? "ɪə" : "ɪ";
                    break;
                case "IY":
                    v = rDropped ? "ɪə" : (s.Stress > 0 ? "iː" : (next == null ? "i" : "ɪ"));
                    break;
                case "UW":
                case "UH":
                    v = rDropped ? "ʊə" : (s.Base == "UW" ? (s.Stress > 0 ? "uː" : "u") : "ʊ");
                    break;
                case "AA":
                    v = next?.Base == "R" ? "ɑː" : "ɒ";
                    break;
                default:
                    v = rDropped && GbRColored.TryGetValue(s.Base, out string? colored) ? colored : GbVowels[s.Base];
                    break;
            }
            // misaki's British dictionaries rarely mark secondary stress.
            sb.Append(s.Stress == 1 ? "ˈ" : "").Append(v);
        }
        return sb.ToString();
    }
}
