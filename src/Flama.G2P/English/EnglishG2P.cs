// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.
//
// The token pipeline (sub-tokenization, right-to-left context, compound stress
// resolution) is ported from misaki/en.py (class G2P), https://github.com/hexgrad/misaki,
// Apache License 2.0. spaCy tokenization and tagging are replaced by the lightweight
// rules in EnglishTokenizer. See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Flama.G2P;

namespace Flama.G2P.English;

public enum OovStrategy
{
    SimpleRules,    // Convert letter by letter using rule-based heuristics
    ReturnOriginal, // Return the original word verbatim
    Throw,          // Throw an exception when a word is not found
    Neural          // Predict with the g2p_en neural model, then fall back to SimpleRules
}

/// <summary>
/// English text to Kokoro phonemes, following misaki (the G2P Kokoro-82M was trained with).
/// Lookup order: misaki gold/silver lexicon with stemming, optional CMUdict, then the
/// configured <see cref="OovStrategy"/>.
/// </summary>
public class EnglishG2P : IG2P
{
    /// <summary>Names Flama ships with whose pronunciation the lexicon and model get wrong.</summary>
    private static readonly Dictionary<string, string> BuiltInOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Kokoro", "kˈOkəɹO" },
        { "Misaki", "misˈɑki" },
        { "Flama", "flˈɑmə" },
    };

    private readonly EnglishLexicon _lexicon;
    private readonly CmuDictionary? _dictionary;
    private readonly OovStrategy _oovStrategy;

    /// <param name="dictionary">Optional CMUdict, consulted for words misaki does not know.</param>
    /// <param name="oovStrategy">What to do with words found in no dictionary.</param>
    /// <param name="british">Use misaki's British lexicon and phoneme set (Kokoro "b" voices).</param>
    public EnglishG2P(CmuDictionary? dictionary = null, OovStrategy oovStrategy = OovStrategy.Neural, bool british = false)
    {
        _dictionary = dictionary;
        _oovStrategy = oovStrategy;
        British = british;
        _lexicon = EnglishLexicon.Get(british);
    }

    public bool British { get; }

    /// <summary>
    /// Emit Kokoro v1.0 symbols: flap as <c>T</c> and glottal stop as <c>t</c> (misaki before 2.0).
    /// Set to false for raw misaki 2.0 output (<c>ɾ</c>, <c>ʔ</c>).
    /// </summary>
    public bool KokoroV1Symbols { get; init; } = true;

    public string Phonemize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string normalized = EnglishTextNormalizer.Normalize(text);
        var tokens = EnglishTokenizer.Tokenize(normalized);
        EnglishTokenizer.AssignTags(tokens);
        var words = Retokenize(tokens);

        var ctx = new TokenContext();
        for (int i = words.Count - 1; i >= 0; i--)
        {
            if (words[i] is EnglishToken single)
            {
                single.Phonemes ??= Override(single.Text) ?? _lexicon.Phonemize(single.Text, single.Tag, ctx) ?? Fallback(single.Text);
                ctx = NextContext(ctx, single.Phonemes, single);
            }
            else
            {
                ctx = ResolveGroup((List<EnglishToken>)words[i], ctx);
            }
        }

        var sb = new StringBuilder();
        foreach (object w in words)
        {
            if (w is EnglishToken tk)
            {
                sb.Append(tk.Phonemes).Append(tk.Whitespace);
                continue;
            }
            var group = (List<EnglishToken>)w;
            foreach (var part in group)
            {
                if (part.Prespace && sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && !string.IsNullOrEmpty(part.Phonemes))
                {
                    sb.Append(' ');
                }
                sb.Append(part.Phonemes);
            }
            sb.Append(group[^1].Whitespace);
        }

        string result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return KokoroV1Symbols ? EnglishPhonemes.ToKokoroV1(result) : result;
    }

    /// <summary>Context-free lookup of a single word (no fallback). Returns false if unknown.</summary>
    public bool TryLookupWord(string word, out string ipa)
    {
        string? ps = _lexicon.Phonemize(word, null, new TokenContext()) ?? LookupCmu(word);
        ipa = ps == null ? string.Empty : (KokoroV1Symbols ? EnglishPhonemes.ToKokoroV1(ps) : ps);
        return ps != null;
    }

    // ------------------------------------------------------------------
    // misaki G2P.retokenize / resolve loop
    // ------------------------------------------------------------------

    private static List<object> Retokenize(List<EnglishToken> tokens)
    {
        var words = new List<object>();
        foreach (var token in tokens)
        {
            List<EnglishToken> parts = token.Phonemes == null
                ? EnglishTokenizer.Subtokenize(token)
                : new List<EnglishToken> { token };

            foreach (var tk in parts)
            {
                if (tk.Phonemes != null)
                {
                    words.Add(tk);
                }
                else if (words.Count > 0 && words[^1] is List<EnglishToken> open && open[^1].Whitespace.Length == 0)
                {
                    tk.IsHead = false;
                    open.Add(tk);
                }
                else
                {
                    words.Add(tk.Whitespace.Length > 0 ? tk : new List<EnglishToken> { tk });
                }
            }
        }

        for (int i = 0; i < words.Count; i++)
        {
            if (words[i] is List<EnglishToken> { Count: 1 } one) words[i] = one[0];
        }
        return words;
    }

    private TokenContext ResolveGroup(List<EnglishToken> w, TokenContext ctx)
    {
        int left = 0, right = w.Count;
        bool shouldFallback = false;
        while (left < right)
        {
            string? ps = null;
            EnglishToken? merged = null;
            if (w.Skip(left).Take(right - left).All(t => t.Phonemes == null))
            {
                merged = Merge(w, left, right);
                ps = Override(merged.Text) ?? _lexicon.Phonemize(merged.Text, merged.Tag, ctx);
            }

            if (ps != null)
            {
                w[left].Phonemes = ps;
                for (int k = left + 1; k < right; k++) w[k].Phonemes = string.Empty;
                ctx = NextContext(ctx, ps, merged!);
                right = left;
                left = 0;
            }
            else if (left + 1 < right)
            {
                left++;
            }
            else
            {
                right--;
                var tk = w[right];
                if (tk.Phonemes == null)
                {
                    if (tk.Text.All(c => EnglishPhonemes.SubtokenJunks.IndexOf(c) >= 0))
                    {
                        tk.Phonemes = string.Empty;
                    }
                    else
                    {
                        shouldFallback = true;
                        break;
                    }
                }
                left = 0;
            }
        }

        if (shouldFallback)
        {
            var whole = Merge(w, 0, w.Count);
            w[0].Phonemes = Fallback(whole.Text);
            for (int k = 1; k < w.Count; k++) w[k].Phonemes = string.Empty;
            return NextContext(ctx, w[0].Phonemes, whole);
        }

        ResolveTokens(w);
        return ctx;
    }

    private static EnglishToken Merge(List<EnglishToken> w, int left, int right)
    {
        var sb = new StringBuilder();
        for (int k = left; k < right; k++)
        {
            sb.Append(w[k].Text);
            if (k < right - 1) sb.Append(w[k].Whitespace);
        }
        return new EnglishToken(sb.ToString(), w[right - 1].Whitespace) { Tag = w[left].Tag };
    }

    /// <summary>misaki resolve_tokens: spacing and stress balance inside one written word.</summary>
    private static void ResolveTokens(List<EnglishToken> tokens)
    {
        string text = Merge(tokens, 0, tokens.Count).Text;
        var classes = new HashSet<int>();
        foreach (char c in text)
        {
            if (EnglishPhonemes.SubtokenJunks.IndexOf(c) >= 0) continue;
            classes.Add(char.IsLetter(c) ? 0 : (c >= '0' && c <= '9' ? 1 : 2));
        }
        bool prespace = text.Contains(' ') || text.Contains('/') || classes.Count > 1;

        for (int i = 0; i < tokens.Count; i++)
        {
            var tk = tokens[i];
            if (tk.Phonemes == null)
            {
                if (i == tokens.Count - 1 && tk.Text.Length > 0 && EnglishPhonemes.NonQuotePuncts.Contains(tk.Text))
                {
                    tk.Phonemes = tk.Text;
                }
                else if (tk.Text.All(c => EnglishPhonemes.SubtokenJunks.IndexOf(c) >= 0))
                {
                    tk.Phonemes = string.Empty;
                }
            }
            else if (i > 0)
            {
                tk.Prespace = prespace;
            }
        }
        if (prespace) return;

        var indices = tokens
            .Select((tk, i) => (tk, i))
            .Where(x => !string.IsNullOrEmpty(x.tk.Phonemes))
            .Select(x => (Primary: x.tk.Phonemes!.IndexOf(EnglishPhonemes.PrimaryStress) >= 0 ? 1 : 0,
                          Weight: EnglishPhonemes.StressWeight(x.tk.Phonemes), Index: x.i))
            .ToList();

        if (indices.Count == 2 && tokens[indices[0].Index].Text.Length == 1)
        {
            var target = tokens[indices[1].Index];
            target.Phonemes = EnglishPhonemes.ApplyStress(target.Phonemes, -0.5);
            return;
        }
        if (indices.Count < 2 || indices.Sum(x => x.Primary) <= (indices.Count + 1) / 2) return;

        foreach (var x in indices.OrderBy(x => x.Primary).ThenBy(x => x.Weight).ThenBy(x => x.Index).Take(indices.Count / 2))
        {
            tokens[x.Index].Phonemes = EnglishPhonemes.ApplyStress(tokens[x.Index].Phonemes, -0.5);
        }
    }

    /// <summary>misaki token_context: does the following word start with a vowel, and is it "to"?</summary>
    private static TokenContext NextContext(TokenContext ctx, string? ps, EnglishToken token)
    {
        bool? vowel = ctx.FutureVowel;
        if (!string.IsNullOrEmpty(ps))
        {
            foreach (char c in ps)
            {
                if (EnglishPhonemes.NonQuotePuncts.IndexOf(c) >= 0) { vowel = null; break; }
                if (EnglishPhonemes.IsVowel(c)) { vowel = true; break; }
                if (EnglishPhonemes.Consonants.IndexOf(c) >= 0) { vowel = false; break; }
            }
        }
        bool futureTo = token.Text is "to" or "To" || (token.Text == "TO" && token.Tag is "TO" or "IN");
        return new TokenContext(vowel, futureTo);
    }

    // ------------------------------------------------------------------
    // Out-of-vocabulary handling
    // ------------------------------------------------------------------

    private string? Override(string word)
    {
        if (!BuiltInOverrides.TryGetValue(word, out string? ps)) return null;
        return British ? ps.Replace('O', 'Q').Replace("ɑ", "ɑː") : ps;
    }

    private string? LookupCmu(string word)
    {
        if (_dictionary == null) return null;
        string w = word.Replace('’', '\'').Replace('‘', '\'');
        if (_dictionary.TryLookup(w, out string[] arpa) && arpa.Length > 0)
        {
            return ArpabetToIpa.ConvertWord(arpa, British);
        }
        return null;
    }

    private string Fallback(string word)
    {
        string? cmu = LookupCmu(word);
        if (cmu != null) return cmu;

        // Nothing pronounceable (stray symbols) stays silent.
        if (!word.Any(char.IsLetter)) return string.Empty;

        switch (_oovStrategy)
        {
            case OovStrategy.Neural:
            {
                // Lowercase consonant clusters are initialisms ("fps", "html"): spell them.
                if (word.Length <= 6 && word.All(char.IsLetter) && !word.Any(c => "aeiouyAEIOUY".IndexOf(c) >= 0))
                {
                    string? spelled = _lexicon.GetNnp(word);
                    if (spelled != null) return spelled;
                }
                string[] arpa = NeuralG2P.Default.Predict(word);
                return arpa.Length > 0 ? ArpabetToIpa.ConvertWord(arpa, British) : ConvertOovLetters(word);
            }
            case OovStrategy.SimpleRules:
                return ConvertOovLetters(word);
            case OovStrategy.Throw:
                throw new KeyNotFoundException($"The word '{word}' was not found in the English lexicon.");
            default:
                return word;
        }
    }

    /// <summary>
    /// Last-resort letter-by-letter mapping. Not linguistically accurate; prefer <see cref="OovStrategy.Neural"/>.
    /// </summary>
    private static string ConvertOovLetters(string word)
    {
        var sb = new StringBuilder();
        string lower = word.ToLowerInvariant();

        for (int i = 0; i < lower.Length; i++)
        {
            char ch = lower[i];
            char next = i + 1 < lower.Length ? lower[i + 1] : '\0';

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
            }
        }

        return sb.ToString();
    }
}
