// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.
//
// SUBTOKEN_REGEX is taken from misaki/en.py (https://github.com/hexgrad/misaki, Apache-2.0).
// The pre-tokenizer and tagger are small rule-based stand-ins for spaCy.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

public sealed class EnglishToken
{
    public EnglishToken(string text, string whitespace)
    {
        Text = text;
        Whitespace = whitespace;
    }

    public string Text { get; }

    /// <summary>"" or " " following the token in the source text.</summary>
    public string Whitespace { get; set; }

    /// <summary>Penn Treebank style tag when a heuristic is confident, otherwise null.</summary>
    public string? Tag { get; set; }

    /// <summary>Set up front for punctuation; filled in by the G2P for words.</summary>
    public string? Phonemes { get; set; }

    public bool IsHead { get; set; } = true;

    public bool Prespace { get; set; }

    public bool IsWord => Phonemes == null;
}

public static class EnglishTokenizer
{
    private static readonly Regex ChunkRegex = new(@"(\S+)(\s*)", RegexOptions.Compiled);

    private static readonly Regex SubtokenRegex = new(
        @"^['‘’]+|\p{Lu}(?=\p{Lu}\p{Ll})|(?:^-)?(?:\d?[,.]?\d)+|[-_]+|['‘’]{2,}|\p{L}*?(?:['‘’]\p{L})*?\p{Ll}(?=\p{Lu})|\p{L}+(?:['‘’]\p{L})*|[^-_\p{L}'‘’\d]|['‘’]+$",
        RegexOptions.Compiled);

    /// <summary>Dotted letter abbreviations kept whole and spelled out: U.S., a.m., U.K.</summary>
    private static readonly Regex DottedAbbreviation = new(@"^(?:[A-Za-z]\.){2,}$", RegexOptions.Compiled);

    /// <summary>Punctuation split off inside a chunk ("hello,world").</summary>
    private const string HardPunct = ",;:!?\"“”()[]{}…—";

    /// <summary>
    /// Splits normalized text into word and punctuation tokens, roughly like spaCy's
    /// tokenizer: prefixes and suffixes are peeled off, apostrophes and hyphens stay
    /// inside words, and punctuation tokens get their Kokoro symbol up front.
    /// </summary>
    public static List<EnglishToken> Tokenize(string text)
    {
        var tokens = new List<EnglishToken>();
        bool quoteOpen = false;
        foreach (Match m in ChunkRegex.Matches(text))
        {
            string chunk = m.Groups[1].Value;
            string ws = m.Groups[2].Length > 0 ? " " : "";
            SplitChunk(chunk, ws, tokens, ref quoteOpen);
        }

        // "... in the U.S." keeps its dot inside the abbreviation; restore the final pause.
        if (tokens.Count > 0 && tokens[^1].IsWord && DottedAbbreviation.IsMatch(tokens[^1].Text))
        {
            tokens.Add(new EnglishToken(".", "") { Phonemes = "." });
        }
        return tokens;
    }

    private static bool IsPunctLike(char c) =>
        !char.IsLetterOrDigit(c) && c is not ('\'' or '‘' or '’' or '_');

    private static void SplitChunk(string chunk, string ws, List<EnglishToken> tokens, ref bool quoteOpen)
    {
        // A chunk of nothing but dashes is a pause.
        if (chunk.Length > 0 && chunk.Trim('-', '–', '—').Length == 0)
        {
            tokens.Add(new EnglishToken(chunk, ws) { Phonemes = "—" });
            return;
        }

        int start = 0, end = chunk.Length;
        var prefix = new List<EnglishToken>();
        while (start < end && IsPunctLike(chunk[start]))
        {
            prefix.Add(Punct(chunk[start].ToString(), "", ref quoteOpen, isPrefix: true));
            start++;
        }

        var suffix = new List<char>();
        while (end > start && IsPunctLike(chunk[end - 1]))
        {
            if (chunk[end - 1] == '.' && DottedAbbreviation.IsMatch(chunk[start..end])) break;
            suffix.Add(chunk[end - 1]);
            end--;
        }
        suffix.Reverse();

        tokens.AddRange(prefix);
        if (end > start)
        {
            SplitCore(chunk[start..end], suffix.Count == 0 ? ws : "", tokens, ref quoteOpen);
        }
        for (int i = 0; i < suffix.Count; i++)
        {
            tokens.Add(Punct(suffix[i].ToString(), i == suffix.Count - 1 ? ws : "", ref quoteOpen, isPrefix: false));
        }
    }

    private static void SplitCore(string core, string ws, List<EnglishToken> tokens, ref bool quoteOpen)
    {
        int segStart = 0;
        for (int i = 0; i < core.Length; i++)
        {
            if (HardPunct.IndexOf(core[i]) < 0) continue;
            if (i > segStart) tokens.Add(new EnglishToken(core[segStart..i], ""));
            tokens.Add(Punct(core[i].ToString(), "", ref quoteOpen, isPrefix: null));
            segStart = i + 1;
        }
        if (segStart < core.Length)
        {
            tokens.Add(new EnglishToken(core[segStart..], ws));
        }
        else if (tokens.Count > 0)
        {
            tokens[^1].Whitespace = ws;
        }
    }

    private static EnglishToken Punct(string text, string ws, ref bool quoteOpen, bool? isPrefix)
    {
        string ps;
        switch (text)
        {
            case "\"":
                bool opening = isPrefix ?? !quoteOpen;
                quoteOpen = opening;
                ps = opening ? "“" : "”";
                break;
            case "“":
                quoteOpen = true;
                ps = text;
                break;
            case "”":
                quoteOpen = false;
                ps = text;
                break;
            case "(":
            case "[":
            case "{":
                ps = "(";
                break;
            case ")":
            case "]":
            case "}":
                ps = ")";
                break;
            default:
                ps = EnglishPhonemes.Puncts.Contains(text) ? text : (text is "-" or "–" ? "—" : "");
                break;
        }
        return new EnglishToken(text, ws) { Phonemes = ps };
    }

    /// <summary>misaki subtokenize: splits a written word at case changes, digits and joiners.</summary>
    public static List<EnglishToken> Subtokenize(EnglishToken token)
    {
        var parts = new List<EnglishToken>();
        foreach (Match m in SubtokenRegex.Matches(token.Text))
        {
            parts.Add(new EnglishToken(m.Value, "") { Tag = token.Tag });
        }
        if (parts.Count == 0)
        {
            parts.Add(new EnglishToken(token.Text, "") { Tag = token.Tag, Phonemes = string.Empty });
        }
        parts[^1].Whitespace = token.Whitespace;
        return parts;
    }

    // ------------------------------------------------------------------
    // Heuristic part-of-speech hints (stand-in for spaCy's tagger)
    // ------------------------------------------------------------------

    private static readonly HashSet<string> Determiners = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "this", "these", "those", "my", "your", "his", "her", "its", "our", "their",
        "some", "any", "no", "every", "each", "another", "either", "neither", "such", "whose"
    };

    private static readonly HashSet<string> VerbTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "to", "will", "would", "shall", "should", "can", "could", "may", "might", "must",
        "do", "does", "did", "don't", "doesn't", "didn't", "won't", "wouldn't", "can't", "cannot",
        "couldn't", "shouldn't", "mustn't", "let's", "please",
        "i'll", "you'll", "we'll", "they'll", "he'll", "she'll", "it'll",
        "i'd", "you'd", "we'd", "they'd", "he'd", "she'd"
    };

    private static readonly HashSet<string> PluralSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "i", "you", "we", "they"
    };

    private static readonly HashSet<string> SingularSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "he", "she", "it"
    };

    private static readonly HashSet<string> PerfectAuxiliaries = new(StringComparer.OrdinalIgnoreCase)
    {
        "have", "has", "had", "having", "i've", "you've", "we've", "they've"
    };

    private static readonly HashSet<string> BeForms = new(StringComparer.OrdinalIgnoreCase)
    {
        "is", "are", "was", "were", "be", "been", "being", "am", "i'm", "you're", "we're", "they're",
        "he's", "she's", "it's", "get", "gets", "got", "getting"
    };

    private static readonly HashSet<string> Adverbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "never", "also", "always", "often", "just", "really", "still", "even", "usually",
        "sometimes", "already", "ever", "only", "then", "now", "rarely", "seldom", "finally"
    };

    /// <summary>Words whose dictionary entry distinguishes VBD/VBN from other verb forms.</summary>
    private static readonly HashSet<string> PastParticipleHeteronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "reread", "wound"
    };

    private static readonly HashSet<string> ObjectStarters = new(StringComparer.OrdinalIgnoreCase)
    {
        "it", "this", "that", "them", "him", "her", "me", "us", "you", "the", "a", "an", "my", "your",
        "his", "our", "their", "being", "having"
    };

    /// <summary>
    /// Assigns the few tags misaki's lexicon reacts to (DT, PRP, IN, TO, VB*, NN, JJ).
    /// Tags are only set when the local context is unambiguous enough; otherwise the
    /// lexicon's DEFAULT pronunciation is used.
    /// </summary>
    public static void AssignTags(List<EnglishToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            var tk = tokens[i];
            if (!tk.IsWord) continue;
            string lw = tk.Text.ToLowerInvariant().Replace('’', '\'');
            var next = NextWord(tokens, i);
            string? nextLower = next?.Text.ToLowerInvariant();

            switch (lw)
            {
                case "i" when tk.Text == "I":
                    tk.Tag = "PRP";
                    continue;
                case "a":
                case "an":
                case "the":
                    if (next != null) tk.Tag = "DT";
                    continue;
                case "that":
                    if (next == null) tk.Tag = "DT";
                    continue;
                case "in":
                case "vs":
                    if (next != null) tk.Tag = "IN";
                    continue;
                case "to":
                    if (next != null) tk.Tag = "TO";
                    continue;
                case "used":
                    tk.Tag = TagUsed(tokens, i, nextLower);
                    continue;
            }

            var (prev, skippedAdverb) = PreviousContentWord(tokens, i);
            if (prev == null) continue;
            string pl = prev.Text.ToLowerInvariant().Replace('’', '\'');

            if (VerbTriggers.Contains(pl))
            {
                tk.Tag = "VB";
            }
            else if (PerfectAuxiliaries.Contains(pl))
            {
                tk.Tag = "VBN";
            }
            else if (BeForms.Contains(pl))
            {
                if (PastParticipleHeteronyms.Contains(lw)) tk.Tag = "VBN";
            }
            else if (PluralSubjects.Contains(pl))
            {
                // Present or past is ambiguous ("I read"); VB keeps the base-form entry.
                tk.Tag = "VB";
            }
            else if (SingularSubjects.Contains(pl))
            {
                // Third person: present takes -s, so a bare form is past ("he read").
                tk.Tag = lw.EndsWith('s') ? "VBZ" : "VBD";
            }
            else if (!skippedAdverb && Determiners.Contains(pl))
            {
                tk.Tag = "NN";
            }
        }
    }

    private static string? TagUsed(List<EnglishToken> tokens, int i, string? nextLower)
    {
        if (nextLower != "to") return null;
        var (prev, _) = PreviousContentWord(tokens, i);
        string? pl = prev?.Text.ToLowerInvariant().Replace('’', '\'');
        if (pl == null || !BeForms.Contains(pl)) return "VBD"; // "I used to live"

        // "be used to + noun/-ing" means accustomed (JJ); "be used to + verb" is passive.
        var to = NextWord(tokens, i);
        int toIndex = to == null ? -1 : tokens.IndexOf(to);
        var after = toIndex < 0 ? null : NextWord(tokens, toIndex);
        if (after == null) return "JJ";
        string al = after.Text.ToLowerInvariant();
        return ObjectStarters.Contains(al) || al.EndsWith("ing", StringComparison.Ordinal) ? "JJ" : "VBN";
    }

    /// <summary>The next word token, or null if punctuation or the end comes first.</summary>
    private static EnglishToken? NextWord(List<EnglishToken> tokens, int i)
    {
        if (i + 1 >= tokens.Count) return null;
        var next = tokens[i + 1];
        if (next.IsWord) return next;
        // Quotes and empty (silent) symbols do not end a phrase.
        return next.Phonemes is "" or "“" or "”" && i + 2 < tokens.Count && tokens[i + 2].IsWord ? tokens[i + 2] : null;
    }

    /// <summary>The preceding word in the same phrase, skipping one adverb such as "not".</summary>
    private static (EnglishToken? Word, bool SkippedAdverb) PreviousContentWord(List<EnglishToken> tokens, int i)
    {
        if (i < 1 || !tokens[i - 1].IsWord) return (null, false);
        var prev = tokens[i - 1];
        if (!Adverbs.Contains(prev.Text)) return (prev, false);
        return i >= 2 && tokens[i - 2].IsWord ? (tokens[i - 2], true) : (null, true);
    }
}
