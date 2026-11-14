// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.
//
// Ported from misaki/en.py (class Lexicon), https://github.com/hexgrad/misaki,
// Copyright hexgrad, Apache License 2.0. The gold/silver dictionaries embedded under
// English/Data are misaki's us_*/gb_* JSON files, unmodified apart from gzip compression.
// See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

/// <summary>Right-to-left context misaki carries while phonemizing a sentence.</summary>
public readonly record struct TokenContext(bool? FutureVowel = null, bool FutureTo = false);

/// <summary>
/// misaki's English lexicon: gold and silver dictionaries plus the special cases,
/// capitalization rules and -s / -ed / -ing stemming that Kokoro's training data used.
/// </summary>
public sealed class EnglishLexicon
{
    private sealed class Entry
    {
        public string? Phonemes;
        public Dictionary<string, string?>? ByTag;
    }

    private static readonly Lazy<EnglishLexicon> UsLexicon = new(() => new EnglishLexicon(british: false));
    private static readonly Lazy<EnglishLexicon> GbLexicon = new(() => new EnglishLexicon(british: true));

    /// <summary>Shared, lazily loaded lexicon (the dictionaries are ~180k entries per dialect).</summary>
    public static EnglishLexicon Get(bool british) => british ? GbLexicon.Value : UsLexicon.Value;

    private static readonly Dictionary<string, string> Symbols = new()
    {
        { "%", "percent" }, { "&", "and" }, { "+", "plus" }, { "@", "at" }
    };

    private static readonly Regex DoubledConsonantIng = new(@"([bcdgklmnprstvxz])\1ing$|cking$", RegexOptions.Compiled);
    private static readonly Regex DoubledConsonantEd = new(@"([bcdgklmnprstvz])\1ed$", RegexOptions.Compiled);
    private static readonly Regex VersusRegex = new(@"^vs\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, Entry> _golds;
    private readonly Dictionary<string, string> _silvers;

    public bool British { get; }

    private EnglishLexicon(bool british)
    {
        British = british;
        string prefix = british ? "gb" : "us";
        _golds = LoadGold($"misaki_{prefix}_gold.json.gz");
        _silvers = LoadSilver($"misaki_{prefix}_silver.json.gz");
    }

    public int GoldCount => _golds.Count;

    public int SilverCount => _silvers.Count;

    // ------------------------------------------------------------------
    // Dictionary access (misaki grow_dictionary, applied lazily)
    // ------------------------------------------------------------------

    private bool TryGetGold(string word, out Entry entry)
    {
        if (_golds.TryGetValue(word, out entry!)) return true;
        string? alt = GrownKey(word);
        return alt != null && _golds.TryGetValue(alt, out entry!);
    }

    private bool TryGetSilver(string word, out string ps)
    {
        if (_silvers.TryGetValue(word, out ps!)) return true;
        string? alt = GrownKey(word);
        return alt != null && _silvers.TryGetValue(alt, out ps!);
    }

    /// <summary>
    /// misaki adds "Word" for every lowercase key and "word" for every capitalized key,
    /// keeping original keys first. Returns the key that would have produced <paramref name="word"/>.
    /// </summary>
    private static string? GrownKey(string word)
    {
        if (word.Length < 2) return null;
        string lower = word.ToLowerInvariant();
        if (word == lower)
        {
            string cap = Capitalize(word);
            return cap != word ? cap : null;
        }
        return word == Capitalize(lower) ? lower : null;
    }

    private bool InGolds(string word) => TryGetGold(word, out _);

    private bool InSilvers(string word) => TryGetSilver(word, out _);

    /// <summary>True when the gold or silver dictionary has an entry for the exact word.</summary>
    public bool Contains(string word) => InGolds(word) || InSilvers(word);

    // ------------------------------------------------------------------
    // Public entry point (misaki Lexicon.__call__ without number/currency handling,
    // which EnglishTextNormalizer performs before tokenization)
    // ------------------------------------------------------------------

    public string? Phonemize(string text, string? tag, TokenContext ctx)
    {
        string word = text.Replace('‘', '\'').Replace('’', '\'').Normalize(NormalizationForm.FormKC);
        double? stress = word == word.ToLowerInvariant()
            ? null
            : (word == word.ToUpperInvariant() ? 2 : 0.5);
        return GetWord(word, tag, stress, ctx);
    }

    // ------------------------------------------------------------------
    // misaki Lexicon internals
    // ------------------------------------------------------------------

    public string? GetNnp(string word)
    {
        var sb = new StringBuilder();
        foreach (char c in word)
        {
            if (!char.IsLetter(c)) continue;
            if (!TryGetGold(char.ToUpperInvariant(c).ToString(), out Entry e)) return null;
            string? letter = e.Phonemes ?? DefaultOf(e);
            if (letter == null) return null;
            sb.Append(letter);
        }
        string? ps = EnglishPhonemes.ApplyStress(sb.ToString(), 0);
        if (ps == null) return null;
        int last = ps.LastIndexOf(EnglishPhonemes.SecondaryStress);
        return last < 0 ? ps : ps[..last] + EnglishPhonemes.PrimaryStress + ps[(last + 1)..];
    }

    private static string? DefaultOf(Entry e) =>
        e.ByTag != null && e.ByTag.TryGetValue("DEFAULT", out string? d) ? d : null;

    private string? GoldDefault(string word, string? tagKey = null)
    {
        if (!TryGetGold(word, out Entry e)) return null;
        if (e.Phonemes != null) return e.Phonemes;
        if (tagKey != null && e.ByTag!.TryGetValue(tagKey, out string? tagged)) return tagged;
        return DefaultOf(e);
    }

    private string? GetSpecialCase(string word, string? tag, double? stress, TokenContext ctx)
    {
        if (Symbols.TryGetValue(word, out string? symbolWord))
        {
            return Lookup(symbolWord, null, null, ctx);
        }

        string stripped = word.Trim('.');
        if (stripped.Contains('.') && IsAlpha(word.Replace(".", "")) && word.Split('.').Max(p => p.Length) < 3)
        {
            return GetNnp(word);
        }

        switch (word)
        {
            case "a":
            case "A":
                return tag == "DT" ? "ɐ" : "ˈA";
            case "am":
            case "Am":
            case "AM":
                if (tag != null && tag.StartsWith("NN", StringComparison.Ordinal)) return GetNnp(word);
                if (ctx.FutureVowel == null || word != "am" || (stress != null && stress > 0)) return GoldDefault("am");
                return "ɐm";
            case "an":
            case "An":
            case "AN":
                if (word == "AN" && tag != null && tag.StartsWith("NN", StringComparison.Ordinal)) return GetNnp(word);
                return "ɐn";
        }

        if (word == "I" && tag == "PRP") return EnglishPhonemes.SecondaryStress + "I";
        if ((word is "by" or "By" or "BY") && ParentTag(tag) == "ADV") return "bˈI";
        if (word is "to" or "To" || (word == "TO" && tag is "TO" or "IN"))
        {
            return ctx.FutureVowel switch
            {
                null => GoldDefault("to"),
                false => "tə",
                true => "tʊ"
            };
        }
        if (word is "in" or "In" || (word == "IN" && tag != "NNP"))
        {
            string mark = ctx.FutureVowel == null || tag != "IN" ? EnglishPhonemes.PrimaryStress.ToString() : "";
            return mark + "ɪn";
        }
        if (word is "the" or "The" || (word == "THE" && tag == "DT"))
        {
            return ctx.FutureVowel == true ? "ði" : "ðə";
        }
        if (tag == "IN" && VersusRegex.IsMatch(word))
        {
            return Lookup("versus", null, null, ctx);
        }
        if (word is "used" or "Used" or "USED")
        {
            return (tag is "VBD" or "JJ") && ctx.FutureTo ? GoldDefault("used", "VBD") : GoldDefault("used");
        }
        return null;
    }

    public static string? ParentTag(string? tag)
    {
        if (tag == null) return null;
        if (tag.StartsWith("VB", StringComparison.Ordinal)) return "VERB";
        if (tag.StartsWith("NN", StringComparison.Ordinal)) return "NOUN";
        if (tag.StartsWith("ADV", StringComparison.Ordinal) || tag.StartsWith("RB", StringComparison.Ordinal)) return "ADV";
        if (tag.StartsWith("ADJ", StringComparison.Ordinal) || tag.StartsWith("JJ", StringComparison.Ordinal)) return "ADJ";
        return tag;
    }

    public bool IsKnown(string word, string? tag)
    {
        if (InGolds(word) || Symbols.ContainsKey(word) || InSilvers(word)) return true;
        if (!IsAlpha(word) || !word.All(IsLexiconChar)) return false;
        if (word.Length == 1) return true;
        if (word == word.ToUpperInvariant() && InGolds(word.ToLowerInvariant())) return true;
        string tail = word[1..];
        return tail == tail.ToUpperInvariant();
    }

    public string? Lookup(string word, string? tag, double? stress, TokenContext? ctx)
    {
        bool isNnp = false;
        if (word == word.ToUpperInvariant() && !InGolds(word))
        {
            word = word.ToLowerInvariant();
            isNnp = tag == "NNP";
        }

        string? ps = null;
        Entry? entry = null;
        if (TryGetGold(word, out Entry gold))
        {
            entry = gold;
        }
        else if (!isNnp && TryGetSilver(word, out string silver))
        {
            ps = silver;
        }

        if (entry != null)
        {
            if (entry.Phonemes != null)
            {
                ps = entry.Phonemes;
            }
            else
            {
                var byTag = entry.ByTag!;
                string? key = tag;
                if (ctx != null && ctx.Value.FutureVowel == null && byTag.ContainsKey("None"))
                {
                    key = "None";
                }
                else if (key == null || !byTag.ContainsKey(key))
                {
                    key = ParentTag(key);
                }
                ps = key != null && byTag.TryGetValue(key, out string? tagged) ? tagged : DefaultOf(entry);
            }
        }

        if (ps == null || (isNnp && ps.IndexOf(EnglishPhonemes.PrimaryStress) < 0))
        {
            string? nnp = GetNnp(word);
            if (nnp != null) return nnp;
        }
        return EnglishPhonemes.ApplyStress(ps, stress);
    }

    public string? SuffixS(string? stem)
    {
        if (string.IsNullOrEmpty(stem)) return null;
        char last = stem[^1];
        if ("ptkfθ".IndexOf(last) >= 0) return stem + "s";
        if ("szʃʒʧʤ".IndexOf(last) >= 0) return stem + (British ? "ɪ" : "ᵻ") + "z";
        return stem + "z";
    }

    private string? StemS(string word, string? tag, double? stress, TokenContext ctx)
    {
        if (word.Length < 3 || !word.EndsWith('s')) return null;
        string stem;
        if (!word.EndsWith("ss", StringComparison.Ordinal) && IsKnown(word[..^1], tag))
        {
            stem = word[..^1];
        }
        else if ((word.EndsWith("'s", StringComparison.Ordinal) ||
                  (word.Length > 4 && word.EndsWith("es", StringComparison.Ordinal) && !word.EndsWith("ies", StringComparison.Ordinal)))
                 && IsKnown(word[..^2], tag))
        {
            stem = word[..^2];
        }
        else if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal) && IsKnown(word[..^3] + "y", tag))
        {
            stem = word[..^3] + "y";
        }
        else
        {
            return null;
        }
        return SuffixS(Lookup(stem, tag, stress, ctx));
    }

    public string? SuffixEd(string? stem)
    {
        if (string.IsNullOrEmpty(stem)) return null;
        char last = stem[^1];
        if ("pkfθʃsʧ".IndexOf(last) >= 0) return stem + "t";
        if (last == 'd') return stem + (British ? "ɪ" : "ᵻ") + "d";
        if (last != 't') return stem + "d";
        if (British || stem.Length < 2) return stem + "ɪd";
        if (EnglishPhonemes.UsTaus.IndexOf(stem[^2]) >= 0) return stem[..^1] + "ɾᵻd";
        return stem + "ᵻd";
    }

    private string? StemEd(string word, string? tag, double? stress, TokenContext ctx)
    {
        if (word.Length < 4 || !word.EndsWith('d')) return null;
        string stem;
        if (!word.EndsWith("dd", StringComparison.Ordinal) && IsKnown(word[..^1], tag))
        {
            stem = word[..^1];
        }
        else if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal) &&
                 !word.EndsWith("eed", StringComparison.Ordinal) && IsKnown(word[..^2], tag))
        {
            stem = word[..^2];
        }
        else if (word.Length > 5 && DoubledConsonantEd.IsMatch(word) && IsKnown(word[..^3], tag))
        {
            // Not in misaki: "stopped" -> "stop", mirroring its -ing doubling rule.
            stem = word[..^3];
        }
        else
        {
            return null;
        }
        return SuffixEd(Lookup(stem, tag, stress, ctx));
    }

    public string? SuffixIng(string? stem)
    {
        if (string.IsNullOrEmpty(stem)) return null;
        if (British)
        {
            if (stem[^1] is 'ə' or 'ː') return null;
        }
        else if (stem.Length > 1 && stem[^1] == 't' && EnglishPhonemes.UsTaus.IndexOf(stem[^2]) >= 0)
        {
            return stem[..^1] + "ɾɪŋ";
        }
        return stem + "ɪŋ";
    }

    private string? StemIng(string word, string? tag, double? stress, TokenContext ctx)
    {
        if (word.Length < 5 || !word.EndsWith("ing", StringComparison.Ordinal)) return null;
        string stem;
        if (word.Length > 5 && IsKnown(word[..^3], tag))
        {
            stem = word[..^3];
        }
        else if (IsKnown(word[..^3] + "e", tag))
        {
            stem = word[..^3] + "e";
        }
        else if (word.Length > 5 && DoubledConsonantIng.IsMatch(word) && IsKnown(word[..^4], tag))
        {
            stem = word[..^4];
        }
        else
        {
            return null;
        }
        return SuffixIng(Lookup(stem, tag, stress, ctx));
    }

    public string? GetWord(string word, string? tag, double? stress, TokenContext ctx)
    {
        string? special = GetSpecialCase(word, tag, stress, ctx);
        if (special != null) return special;

        string wl = word.ToLowerInvariant();
        if (word.Length > 1 && IsAlpha(word.Replace("'", "")) && word != wl &&
            (tag != "NNP" || word.Length > 7) && !InGolds(word) && !InSilvers(word) &&
            (word == word.ToUpperInvariant() || word[1..] == word[1..].ToLowerInvariant()) &&
            (InGolds(wl) || InSilvers(wl) ||
             StemS(wl, tag, stress, ctx) != null || StemEd(wl, tag, stress, ctx) != null || StemIng(wl, tag, stress, ctx) != null))
        {
            word = wl;
        }

        if (IsKnown(word, tag)) return Lookup(word, tag, stress, ctx);
        if (word.EndsWith("s'", StringComparison.Ordinal) && IsKnown(word[..^2] + "'s", tag))
        {
            return Lookup(word[..^2] + "'s", tag, stress, ctx);
        }
        if (word.EndsWith('\'') && IsKnown(word[..^1], tag)) return Lookup(word[..^1], tag, stress, ctx);

        return StemS(word, tag, stress, ctx)
            ?? StemEd(word, tag, stress, ctx)
            ?? StemIng(word, tag, stress ?? 0.5, ctx);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Python str.isalpha(): non-empty and every char is a letter.</summary>
    internal static bool IsAlpha(string s) => s.Length > 0 && s.All(char.IsLetter);

    /// <summary>misaki LEXICON_ORDS: apostrophe, hyphen and ASCII letters.</summary>
    internal static bool IsLexiconChar(char c) => c == '\'' || c == '-' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    /// <summary>Python str.capitalize() for the ASCII lexicon keys.</summary>
    internal static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    // ------------------------------------------------------------------
    // Loading
    // ------------------------------------------------------------------

    private static byte[] ReadEmbeddedGzip(string fileName)
    {
        var asm = typeof(EnglishLexicon).Assembly;
        string resource = $"Flama.G2P.English.Data.{fileName}";
        using Stream? stream = asm.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Embedded G2P resource '{resource}' is missing.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gzip.CopyTo(ms);
        return ms.ToArray();
    }

    private static Dictionary<string, Entry> LoadGold(string fileName)
    {
        var dict = new Dictionary<string, Entry>(100_000, StringComparer.Ordinal);
        var reader = new Utf8JsonReader(ReadEmbeddedGzip(fileName));
        Expect(ref reader, JsonTokenType.StartObject);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                dict[key] = new Entry { Phonemes = reader.GetString() };
                continue;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidDataException($"Unexpected JSON token for '{key}' in {fileName}.");
            }

            var byTag = new Dictionary<string, string?>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string tagKey = reader.GetString()!;
                reader.Read();
                byTag[tagKey] = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            dict[key] = new Entry { ByTag = byTag };
        }
        return dict;
    }

    private static Dictionary<string, string> LoadSilver(string fileName)
    {
        var dict = new Dictionary<string, string>(120_000, StringComparer.Ordinal);
        var reader = new Utf8JsonReader(ReadEmbeddedGzip(fileName));
        Expect(ref reader, JsonTokenType.StartObject);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                dict[key] = reader.GetString()!;
            }
            else
            {
                reader.Skip();
            }
        }
        return dict;
    }

    private static void Expect(ref Utf8JsonReader reader, JsonTokenType type)
    {
        if (!reader.Read() || reader.TokenType != type)
        {
            throw new InvalidDataException($"Malformed G2P lexicon: expected {type}.");
        }
    }
}
