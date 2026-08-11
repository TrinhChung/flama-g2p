// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flama.G2P.Chinese;

public static class PinyinToIpa
{
    private static readonly Dictionary<string, string[]> InitialMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "b", new[] { "p" } },
        { "c", new[] { "ʦʰ" } },
        { "ch", new[] { "ʈʂʰ" } },
        { "d", new[] { "t" } },
        { "f", new[] { "f" } },
        { "g", new[] { "k" } },
        { "h", new[] { "x" } },
        { "j", new[] { "ʨ" } },
        { "k", new[] { "kʰ" } },
        { "l", new[] { "l" } },
        { "m", new[] { "m" } },
        { "n", new[] { "n" } },
        { "p", new[] { "pʰ" } },
        { "q", new[] { "ʨʰ" } },
        { "r", new[] { "ɻ" } },
        { "s", new[] { "s" } },
        { "sh", new[] { "ʂ" } },
        { "t", new[] { "tʰ" } },
        { "x", new[] { "ɕ" } },
        { "z", new[] { "ʦ" } },
        { "zh", new[] { "ʈʂ" } }
    };

    private static readonly Dictionary<string, string[]> SyllabicConsonantMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "hm", new[] { "h", "m0" } },
        { "hng", new[] { "h", "ŋ0" } },
        { "m", new[] { "m0" } },
        { "n", new[] { "n0" } },
        { "ng", new[] { "ŋ0" } }
    };

    private static readonly Dictionary<string, string[]> InterjectionMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "io", new[] { "j", "ɔ0" } },
        { "ê", new[] { "ɛ0" } },
        { "er", new[] { "ɚ0" } },
        { "o", new[] { "ɔ0" } }
    };

    private static readonly Dictionary<string, string[]> FinalMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "a", new[] { "a0" } },
        { "ai", new[] { "ai̯0" } },
        { "an", new[] { "a0", "n" } },
        { "ang", new[] { "a0", "ŋ" } },
        { "ao", new[] { "au̯0" } },
        { "e", new[] { "ɤ0" } },
        { "ei", new[] { "ei̯0" } },
        { "en", new[] { "ə0", "n" } },
        { "eng", new[] { "ə0", "ŋ" } },
        { "i", new[] { "i0" } },
        { "ia", new[] { "j", "a0" } },
        { "ian", new[] { "j", "ɛ0", "n" } },
        { "iang", new[] { "j", "a0", "ŋ" } },
        { "iao", new[] { "j", "au̯0" } },
        { "ie", new[] { "j", "e0" } },
        { "in", new[] { "i0", "n" } },
        { "iou", new[] { "j", "ou̯0" } },
        { "ing", new[] { "i0", "ŋ" } },
        { "iong", new[] { "j", "ɔ0", "ŋ" } },
        { "ong", new[] { "ɔ0", "ŋ" } },
        { "ou", new[] { "ou̯0" } },
        { "u", new[] { "u0" } },
        { "uei", new[] { "w", "ei̯0" } },
        { "ua", new[] { "w", "a0" } },
        { "uai", new[] { "w", "ai̯0" } },
        { "uan", new[] { "w", "a0", "n" } },
        { "uen", new[] { "w", "ə0", "n" } },
        { "uang", new[] { "w", "a0", "ŋ" } },
        { "ueng", new[] { "w", "ə0", "ŋ" } },
        { "uo", new[] { "w", "o0" } },
        { "o", new[] { "w", "o0" } },
        { "ü", new[] { "y0" } },
        { "üe", new[] { "ɥ", "e0" } },
        { "üan", new[] { "ɥ", "ɛ0", "n" } },
        { "ün", new[] { "y0", "n" } }
    };

    private static readonly Dictionary<string, string[]> FinalMappingAfterZhChShR = new(StringComparer.OrdinalIgnoreCase)
    {
        { "i", new[] { "ɻ̩0" } }
    };

    private static readonly Dictionary<string, string[]> FinalMappingAfterZCS = new(StringComparer.OrdinalIgnoreCase)
    {
        { "i", new[] { "ɹ̩0" } }
    };

    private static readonly Dictionary<int, string> ToneMapping = new()
    {
        { 1, "˥" },
        { 2, "˧˥" },
        { 3, "˧˩˧" },
        { 4, "˥˩" },
        { 5, "" }
    };

    public static string Convert(string pinyin)
    {
        if (string.IsNullOrWhiteSpace(pinyin)) return string.Empty;

        // 1. Normalize orthographic abbreviations (ui->uei, iu->iou, un->uen)
        string normalized = NormalizePinyinOrthography(pinyin);

        // 2. Normalize j/q/x + u -> j/q/x + ü rules
        normalized = NormalizeJqxu(normalized);

        // 3. Extract tone (last digit 1-5)
        int tone = 5;
        string normalPinyin = normalized;
        if (normalized.Length > 1 && char.IsDigit(normalized[^1]))
        {
            if (int.TryParse(normalized[^1].ToString(), out int parsedTone))
            {
                tone = parsedTone;
                normalPinyin = normalized[..^1];
            }
        }

        if (!ToneMapping.ContainsKey(tone)) tone = 5;

        // 4. Check Syllabic Consonants and Interjections
        if (SyllabicConsonantMappings.TryGetValue(normalPinyin, out var syllabic))
        {
            return ApplyTone(syllabic, tone);
        }

        if (InterjectionMappings.TryGetValue(normalPinyin, out var interj))
        {
            return ApplyTone(interj, tone);
        }

        // 5. Split Initial & Final
        var (initial, final) = SplitInitialFinal(normalPinyin);

        // 6. Map Initial to IPA
        string initialIpa = "";
        if (initial != null)
        {
            if (InitialMapping.TryGetValue(initial, out var initIpaArr))
            {
                initialIpa = initIpaArr[0];
            }
        }

        // 7. Map Final to IPA
        string finalIpa = "";
        if (final != null)
        {
            string[]? finalIpaArr = null;

            if (initial != null && (initial == "zh" || initial == "ch" || initial == "sh" || initial == "r"))
            {
                FinalMappingAfterZhChShR.TryGetValue(final, out finalIpaArr);
            }
            else if (initial != null && (initial == "z" || initial == "c" || initial == "s"))
            {
                FinalMappingAfterZCS.TryGetValue(final, out finalIpaArr);
            }

            if (finalIpaArr == null)
            {
                FinalMapping.TryGetValue(final, out finalIpaArr);
            }

            if (finalIpaArr != null)
            {
                finalIpa = ApplyTone(finalIpaArr, tone);
            }
            else
            {
                finalIpa = final;
            }
        }

        return initialIpa + finalIpa;
    }

    /// <summary>
    /// Normalizes Binh Am written abbreviations: ui -> uei, iu -> iou, un -> uen (under strict initial contexts).
    /// </summary>
    public static string NormalizePinyinOrthography(string pinyin)
    {
        if (string.IsNullOrWhiteSpace(pinyin)) return pinyin;

        string lower = pinyin.ToLowerInvariant();
        string tone = "";
        string normal = lower;
        if (lower.Length > 1 && char.IsDigit(lower[^1]))
        {
            tone = lower[^1].ToString();
            normal = lower[..^1];
        }

        if (normal.EndsWith("ui"))
        {
            normal = normal[..^2] + "uei";
        }
        else if (normal.EndsWith("iu"))
        {
            normal = normal[..^2] + "iou";
        }
        else if (normal.EndsWith("un"))
        {
            // Only convert to uen if it doesn't start with j/q/x/y which represent 'ün'
            bool isJqxy = normal.StartsWith("j") || normal.StartsWith("q") || normal.StartsWith("x") || normal.StartsWith("y");
            if (!isJqxy)
            {
                normal = normal[..^2] + "uen";
            }
        }

        return normal + tone;
    }

    /// <summary>
    /// Normalizes the j/q/x + u -> j/q/x + ü orthographic rule.
    /// </summary>
    public static string NormalizeJqxu(string pinyin)
    {
        if (string.IsNullOrWhiteSpace(pinyin)) return pinyin;

        string lower = pinyin.ToLowerInvariant();
        string tone = "";
        string normal = lower;
        if (lower.Length > 1 && char.IsDigit(lower[^1]))
        {
            tone = lower[^1].ToString();
            normal = lower[..^1];
        }

        if (normal.StartsWith("j") || normal.StartsWith("q") || normal.StartsWith("x"))
        {
            if (normal.Length >= 2 && normal[1] == 'u')
            {
                normal = normal[0] + "ü" + normal[2..];
            }
        }

        return normal + tone;
    }

    private static string ApplyTone(string[] phonemes, int tone)
    {
        string toneSymbol = ToneMapping[tone];
        return string.Concat(phonemes.Select(p => p.Replace("0", toneSymbol)));
    }

    private static (string? Initial, string Final) SplitInitialFinal(string pinyin)
    {
        if (pinyin.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            pinyin.StartsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            pinyin.StartsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return (pinyin[..2], pinyin[2..]);
        }

        string[] strictInitials = { "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h", "j", "q", "x", "r", "z", "c", "s" };
        foreach (var init in strictInitials)
        {
            if (pinyin.StartsWith(init, StringComparison.OrdinalIgnoreCase))
            {
                return (init, pinyin[init.Length..]);
            }
        }

        if (pinyin.StartsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            return pinyin.ToLowerInvariant() switch
            {
                "yi" => (null, "i"),
                "ya" => (null, "ia"),
                "yan" => (null, "ian"),
                "yang" => (null, "iang"),
                "yao" => (null, "iao"),
                "ye" => (null, "ie"),
                "yin" => (null, "in"),
                "ying" => (null, "ing"),
                "yong" => (null, "iong"),
                "yung" => (null, "iong"),
                "you" => (null, "iou"),
                "yu" => (null, "ü"),
                "yue" => (null, "üe"),
                "yuan" => (null, "üan"),
                "yun" => (null, "ün"),
                _ => (null, pinyin[1..])
            };
        }

        if (pinyin.StartsWith("w", StringComparison.OrdinalIgnoreCase))
        {
            return pinyin.ToLowerInvariant() switch
            {
                "wu" => (null, "u"),
                "wa" => (null, "ua"),
                "wai" => (null, "uai"),
                "wan" => (null, "uan"),
                "wen" => (null, "uen"),
                "wang" => (null, "uang"),
                "weng" => (null, "ueng"),
                "wo" => (null, "uo"),
                _ => (null, pinyin[1..])
            };
        }

        return (null, pinyin);
    }
}
