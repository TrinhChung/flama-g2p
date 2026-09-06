// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

public static class EnglishTextNormalizer
{
    private static readonly string[] Ones =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
    };

    private static readonly string[] Tens =
    {
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
    };

    private static readonly Dictionary<string, string> OrdinalExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "one", "first" },
        { "two", "second" },
        { "three", "third" },
        { "five", "fifth" },
        { "eight", "eighth" },
        { "nine", "ninth" },
        { "twelve", "twelfth" }
    };

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Normalize quotes and punctuation
        text = text.Replace('“', '"').Replace('”', '"')
                   .Replace('‘', '\'').Replace('’', '\'')
                   .Replace('«', '"').Replace('»', '"')
                   .Replace("—", " — ")
                   .Replace("–", " - ")
                   .Replace("…", " ... ");

        // 2. Normalize symbols
        text = Regex.Replace(text, @"(?<=\d|\w)\s*%\b|(?<=\d)\s*%", " percent ");
        text = Regex.Replace(text, @"\s*&\s*", " and ");
        text = Regex.Replace(text, @"\s*\+\s*", " plus ");
        text = Regex.Replace(text, @"(?<=\w)\s*@\s*(?=\w)", " at ");

        // 3. Normalize currencies: $20, $19.99, £5, €10, ¥100
        text = NormalizeCurrencies(text);

        // 4. Normalize ordinals: 1st, 2nd, 3rd, 21st, 100th
        text = Regex.Replace(text, @"\b(\d+)(st|nd|rd|th)\b", m =>
        {
            if (long.TryParse(m.Groups[1].Value, out long val))
            {
                return NumberToOrdinal(val);
            }
            return m.Value;
        }, RegexOptions.IgnoreCase);

        // 5. Normalize 4-digit years: 1000 - 2099 (e.g. 1998 -> nineteen ninety-eight, 2024 -> twenty twenty-four)
        text = Regex.Replace(text, @"\b(1[0-9]{3}|20[0-9]{2})\b", m =>
        {
            if (int.TryParse(m.Value, out int year))
            {
                return YearToWords(year);
            }
            return m.Value;
        });

        // 6. Normalize decimals: 3.14 -> three point one four
        text = Regex.Replace(text, @"\b(\d+)\.(\d+)\b", m =>
        {
            string intPart = m.Groups[1].Value;
            string decPart = m.Groups[2].Value;

            var sb = new StringBuilder();
            if (long.TryParse(intPart, out long intVal))
            {
                sb.Append(NumberToWords(intVal));
            }
            else
            {
                sb.Append(intPart);
            }

            sb.Append(" point");
            foreach (char c in decPart)
            {
                if (c >= '0' && c <= '9')
                {
                    sb.Append(' ').Append(Ones[c - '0']);
                }
            }
            return sb.ToString();
        });

        // 7. Normalize integers (supporting commas like 1,000,000)
        text = Regex.Replace(text, @"(?<!\w)-?\b\d{1,3}(?:,\d{3})+\b", m =>
        {
            string raw = m.Value.Replace(",", "");
            if (long.TryParse(raw, out long val))
            {
                return NumberToWords(val);
            }
            return m.Value;
        });

        text = Regex.Replace(text, @"(?<![\w\.])-?\b\d+\b(?![\w\.])", m =>
        {
            if (long.TryParse(m.Value, out long val))
            {
                return NumberToWords(val);
            }
            return m.Value;
        });

        // 8. Clean up extra whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private static string NormalizeCurrencies(string text)
    {
        return Regex.Replace(text, @"(\$|£|€|¥)\s*(\d+(?:,\d{3})*(?:\.\d+)?)", m =>
        {
            string symbol = m.Groups[1].Value;
            string numStr = m.Groups[2].Value.Replace(",", "");

            if (!double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount))
            {
                return m.Value;
            }

            long major = (long)Math.Floor(amount);
            int minor = (int)Math.Round((amount - major) * 100);

            var (majorSingular, majorPlural, minorSingular, minorPlural) = symbol switch
            {
                "$" => ("dollar", "dollars", "cent", "cents"),
                "£" => ("pound", "pounds", "pence", "pence"),
                "€" => ("euro", "euros", "cent", "cents"),
                "¥" => ("yen", "yen", "", ""),
                _ => ("dollar", "dollars", "cent", "cents")
            };

            var sb = new StringBuilder();

            if (major > 0 || minor == 0)
            {
                sb.Append(NumberToWords(major)).Append(' ');
                sb.Append(major == 1 ? majorSingular : majorPlural);
            }

            if (minor > 0 && !string.IsNullOrEmpty(minorSingular))
            {
                if (sb.Length > 0) sb.Append(" and ");
                sb.Append(NumberToWords(minor)).Append(' ');
                sb.Append(minor == 1 ? minorSingular : minorPlural);
            }

            return sb.ToString();
        });
    }

    public static string NumberToWords(long number)
    {
        if (number == 0) return "zero";

        if (number < 0)
        {
            return "minus " + NumberToWords(Math.Abs(number));
        }

        var sb = new StringBuilder();

        if (number >= 1_000_000_000)
        {
            long billions = number / 1_000_000_000;
            sb.Append(NumberToWords(billions)).Append(" billion");
            number %= 1_000_000_000;
            if (number > 0) sb.Append(' ');
        }

        if (number >= 1_000_000)
        {
            long millions = number / 1_000_000;
            sb.Append(NumberToWords(millions)).Append(" million");
            number %= 1_000_000;
            if (number > 0) sb.Append(' ');
        }

        if (number >= 1_000)
        {
            long thousands = number / 1_000;
            sb.Append(NumberToWords(thousands)).Append(" thousand");
            number %= 1_000;
            if (number > 0) sb.Append(' ');
        }

        if (number >= 100)
        {
            long hundreds = number / 100;
            sb.Append(Ones[hundreds]).Append(" hundred");
            number %= 100;
            if (number > 0) sb.Append(' ');
        }

        if (number > 0)
        {
            if (number < 20)
            {
                sb.Append(Ones[number]);
            }
            else
            {
                sb.Append(Tens[number / 10]);
                long rem = number % 10;
                if (rem > 0)
                {
                    sb.Append('-').Append(Ones[rem]);
                }
            }
        }

        return sb.ToString();
    }

    public static string NumberToOrdinal(long number)
    {
        if (number <= 0) return number.ToString();

        string words = NumberToWords(number);

        // Split into parts to modify the last word
        int lastSpace = words.LastIndexOf(' ');
        int lastHyphen = words.LastIndexOf('-');
        int splitIdx = Math.Max(lastSpace, lastHyphen);

        string prefix = splitIdx >= 0 ? words[..(splitIdx + 1)] : "";
        string lastWord = splitIdx >= 0 ? words[(splitIdx + 1)..] : words;

        if (OrdinalExceptions.TryGetValue(lastWord, out string? special))
        {
            return prefix + special;
        }

        if (lastWord.EndsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            return prefix + lastWord[..^1] + "ieth";
        }

        return prefix + lastWord + "th";
    }

    public static string YearToWords(int year)
    {
        if (year < 1000 || year > 2999)
        {
            return NumberToWords(year);
        }

        if (year % 100 == 0)
        {
            int century = year / 100;
            return NumberToWords(century) + " hundred";
        }

        int high = year / 100;
        int low = year % 100;

        if (year >= 2000 && year <= 2009)
        {
            return "two thousand " + Ones[low];
        }

        string highStr = NumberToWords(high);
        string lowStr = low < 10 ? "oh " + Ones[low] : NumberToWords(low);

        return highStr + " " + lowStr;
    }
}
