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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

/// <summary>
/// Rewrites numbers, currencies, times, abbreviations and symbols as English words
/// before phonemization. Number reading follows misaki/num2words conventions
/// (no "and", years read in pairs, "oh" for a zero ten digit).
/// </summary>
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

    /// <summary>Titles that always precede a name: the period is dropped, never read as a pause.</summary>
    private static readonly Dictionary<string, string> TitleAbbreviations = new(StringComparer.Ordinal)
    {
        { "Mr", "Mr" }, { "Mrs", "Mrs" }, { "Ms", "Miz" }, { "Dr", "Dr" }, { "Prof", "Professor" },
        { "St", "Saint" }, { "Mt", "Mount" }, { "Gen", "General" }, { "Sgt", "Sergeant" }, { "Capt", "Captain" }
    };

    /// <summary>Abbreviations that may end a sentence: the period is kept only at the end of a line.</summary>
    private static readonly Dictionary<string, string> TrailingAbbreviations = new(StringComparer.Ordinal)
    {
        { "etc", "etc" }, { "Jr", "Junior" }, { "Sr", "Senior" }, { "Inc", "Incorporated" }, { "Ltd", "Limited" },
        { "Corp", "Corporation" }, { "approx", "approximately" }, { "Ave", "Avenue" }, { "Blvd", "Boulevard" }
    };

    private static readonly (string Pattern, string Singular, string Plural)[] Units =
    {
        ("km/h", "kilometer per hour", "kilometers per hour"),
        ("mph", "mile per hour", "miles per hour"),
        ("kg", "kilogram", "kilograms"),
        ("mg", "milligram", "milligrams"),
        ("km", "kilometer", "kilometers"),
        ("cm", "centimeter", "centimeters"),
        ("mm", "millimeter", "millimeters"),
        ("ml", "milliliter", "milliliters"),
        ("°C", "degree Celsius", "degrees Celsius"),
        ("°F", "degree Fahrenheit", "degrees Fahrenheit"),
        ("°", "degree", "degrees"),
        ("GHz", "gigahertz", "gigahertz"),
        ("MHz", "megahertz", "megahertz"),
        ("kHz", "kilohertz", "kilohertz"),
        ("Hz", "hertz", "hertz"),
        ("TB", "terabyte", "terabytes"),
        ("GB", "gigabyte", "gigabytes"),
        ("MB", "megabyte", "megabytes"),
        ("KB", "kilobyte", "kilobytes"),
        ("kB", "kilobyte", "kilobytes"),
    };

    private static readonly Regex UnitRegex = new(
        @"(?<![\w.])(-?\d+(?:\.\d+)?)\s*(" + string.Join("|", Units.Select(u => Regex.Escape(u.Pattern))) + @")(?![A-Za-z])",
        RegexOptions.Compiled);

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = NormalizeCharacters(text);
        text = NormalizeAbbreviations(text);

        // Symbols
        text = Regex.Replace(text, @"#(?=\d)", "number ");
        text = Regex.Replace(text, @"\s*%", " percent ");
        text = Regex.Replace(text, @"\s*&\s*", " and ");
        text = Regex.Replace(text, @"\s*\+\s*", " plus ");
        text = Regex.Replace(text, @"(?<=\w)\s*@\s*(?=\w)", " at ");

        text = NormalizeCurrencies(text);
        text = NormalizeDates(text);
        text = NormalizeTimes(text);
        text = NormalizeUnits(text);

        // "2" between single capitals reads as "to" (B2B, P2P, G2P), as in misaki; H2O stays a number.
        text = Regex.Replace(text, @"(?<![A-Za-z])([A-Z])2([A-Z])(?![A-Za-z])", m =>
            m.Value is "H2O" or "H2S" ? m.Value : $"{m.Groups[1].Value} to {m.Groups[2].Value}");

        // Split digits glued to letters so they are not dropped (3D, mp3, 4K, v1.5),
        // keeping ordinal and decade suffixes attached (4th, 1990s).
        text = Regex.Replace(text, @"(?<=[A-Za-z])(?=\d)|(?<=\d)(?=[A-Za-z])(?!(?:st|nd|rd|th|s)\b)", " ");

        // Grouped numbers with optional decimals: 1,000,000 / 1,234.5
        text = Regex.Replace(text, @"(?<![\w.,])-?\d{1,3}(?:,\d{3})+(?:\.\d+)?(?![\w,]|\.\d)", m =>
            DecimalToWords(m.Value.Replace(",", "")));

        // Version-like numbers: 1.2.3 -> one point two point three
        text = Regex.Replace(text, @"(?<![\w.])\d+(?:\.\d+){2,}(?!\w|\.\d)", m =>
            string.Join(" point ", m.Value.Split('.').Select(DigitsOrNumber)));

        // Decimals: 3.14 -> three point one four, .5 -> point five
        text = Regex.Replace(text, @"(?<![\w.])-?\d*\.\d+(?!\w|\.\d)", m => DecimalToWords(m.Value));

        // Ordinals: 1st, 2nd, 3rd, 21st, 100th
        text = Regex.Replace(text, @"(?<![\w.])(\d+)(st|nd|rd|th)\b", m =>
            long.TryParse(m.Groups[1].Value, out long val) ? NumberToOrdinal(val) : m.Value,
            RegexOptions.IgnoreCase);

        // Decades: 1990s, '80s, 80s
        text = Regex.Replace(text, @"(?<![\w.])(1[0-9]{3}|20[0-9]{2})'?s\b", m =>
            Pluralize(YearToWords(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))));
        text = Regex.Replace(text, @"(?<![\w.])['’]?([1-9]0)'?s\b", m =>
            Pluralize(NumberToWords(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))));

        // Four-digit years 1000-2099: 1998 -> nineteen ninety-eight, 2024 -> twenty twenty-four
        text = Regex.Replace(text, @"(?<![\w.])(1[0-9]{3}|20[0-9]{2})(?!\w|\.\d)", m =>
            YearToWords(int.Parse(m.Value, CultureInfo.InvariantCulture)));

        // Remaining integers, including negatives
        text = Regex.Replace(text, @"(?<![\w.])-?\d+(?![\w]|\.\d)", m =>
            long.TryParse(m.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long val)
                ? NumberToWords(val)
                : DigitsOrNumber(m.Value));

        text = Regex.Replace(text, @"[ \t ]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n").Trim();

        return text;
    }

    private static string NormalizeCharacters(string text)
    {
        text = text.Replace(' ', ' ')
                   .Replace('‘', '\'').Replace('’', '\'').Replace('`', '\'')
                   .Replace('«', '“').Replace('»', '”')
                   .Replace("—", " — ")
                   .Replace("―", " — ");

        // En dash: a range between numbers reads "to", elsewhere it is a pause.
        text = Regex.Replace(text, @"(?<=\d)\s*–\s*(?=\d)", " to ");
        text = text.Replace("–", " — ");

        // Strip diacritics on Latin letters (café -> cafe, naïve -> naive); misaki's lexicon is ASCII.
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        for (int i = 0; i < decomposed.Length; i++)
        {
            char c = decomposed[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark &&
                i > 0 && decomposed[i - 1] < 0x250)
            {
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeAbbreviations(string text)
    {
        text = Regex.Replace(text, @"\be\.g\.(?=\s|,|$)", "for example", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bi\.e\.(?=\s|,|$)", "that is", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bvs\.?(?=\s)", "versus", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bNo\.\s*(?=\d)", "number ");

        text = Regex.Replace(text, @"\b([A-Z][a-z]{1,3})\.(?=\s+\S)", m =>
            TitleAbbreviations.TryGetValue(m.Groups[1].Value, out string? title) ? title : m.Value);

        text = Regex.Replace(text, @"\b([A-Za-z]{2,6})\.(?=([ \t]*)(\r?\n|$)|\s)", m =>
        {
            if (!TrailingAbbreviations.TryGetValue(m.Groups[1].Value, out string? word)) return m.Value;
            return m.Groups[3].Success ? word + "." : word;
        });
        return text;
    }

    private static string NormalizeCurrencies(string text)
    {
        // $3.5 million -> three point five million dollars
        text = Regex.Replace(text, @"([$£€¥])\s*(\d+(?:\.\d+)?)\s*(thousand|million|billion|trillion)\b", m =>
        {
            var (_, plural, _, _) = CurrencyNames(m.Groups[1].Value);
            return $"{DecimalToWords(m.Groups[2].Value)} {m.Groups[3].Value} {plural}";
        }, RegexOptions.IgnoreCase);

        return Regex.Replace(text, @"([$£€¥])\s*(\d+(?:,\d{3})*(?:\.\d+)?)", m =>
        {
            string numStr = m.Groups[2].Value.Replace(",", "");
            if (!decimal.TryParse(numStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
            {
                return m.Value;
            }

            long major = (long)decimal.Floor(amount);
            int minor = (int)decimal.Round((amount - major) * 100);
            var (majorSingular, majorPlural, minorSingular, minorPlural) = CurrencyNames(m.Groups[1].Value);

            var sb = new StringBuilder();
            if (major > 0 || minor == 0 || string.IsNullOrEmpty(minorSingular))
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

    private static (string MajorSingular, string MajorPlural, string MinorSingular, string MinorPlural) CurrencyNames(string symbol) =>
        symbol switch
        {
            "£" => ("pound", "pounds", "penny", "pence"),
            "€" => ("euro", "euros", "cent", "cents"),
            "¥" => ("yen", "yen", "", ""),
            _ => ("dollar", "dollars", "cent", "cents")
        };

    private static string NormalizeTimes(string text)
    {
        // 10:30 pm / 7am / 7 a.m.
        text = Regex.Replace(text, @"(?<![\w:.])([01]?\d|2[0-3])(?::([0-5]\d))?\s*([AaPp])\.?\s?[Mm]\.?(?![A-Za-z])", m =>
        {
            string hour = NumberToWords(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            string minutes = m.Groups[2].Success ? MinutesToWords(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), allowOClock: false) : "";
            // Dotted form is spelled out by the lexicon ("A.M." -> ˌAˈɛm) instead of reading "a" / "am".
            string suffix = char.ToUpperInvariant(m.Groups[3].Value[0]) + ".M.";
            return string.Join(" ", new[] { hour, minutes, suffix }.Where(s => s.Length > 0));
        });

        // 14:30, 9:05, 3:00
        return Regex.Replace(text, @"(?<![\w:.])([01]?\d|2[0-3]):([0-5]\d)(?!\w|:\d|\.\d)", m =>
        {
            string hour = NumberToWords(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            string minutes = MinutesToWords(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), allowOClock: true);
            return $"{hour} {minutes}";
        });
    }

    private static readonly string[] Months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    /// <summary>ISO dates: 2024-01-15 -> January fifteenth, twenty twenty-four.</summary>
    private static string NormalizeDates(string text)
    {
        return Regex.Replace(text, @"(?<![\w.-])(1[0-9]{3}|20[0-9]{2})-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])(?![\w-])", m =>
        {
            int year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int month = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int day = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return $"{Months[month - 1]} {NumberToOrdinal(day)}, {YearToWords(year)}";
        });
    }

    private static string MinutesToWords(int minutes, bool allowOClock)
    {
        if (minutes == 0) return allowOClock ? "o'clock" : "";
        if (minutes < 10) return "oh " + Ones[minutes];
        return NumberToWords(minutes);
    }

    private static string NormalizeUnits(string text)
    {
        return UnitRegex.Replace(text, m =>
        {
            string unit = m.Groups[2].Value;
            var entry = Units.First(u => u.Pattern == unit);
            bool singular = m.Groups[1].Value is "1" or "-1";
            return $"{m.Groups[1].Value} {(singular ? entry.Singular : entry.Plural)}";
        });
    }

    private static string DigitsOrNumber(string digits)
    {
        if (digits.Length > 1 && digits[0] == '0')
        {
            return string.Join(" ", digits.Where(char.IsDigit).Select(c => Ones[c - '0']));
        }
        return long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long val)
            ? NumberToWords(val)
            : string.Join(" ", digits.Where(char.IsDigit).Select(c => Ones[c - '0']));
    }

    private static string DecimalToWords(string value)
    {
        bool negative = value.StartsWith('-');
        if (negative) value = value[1..];
        int dot = value.IndexOf('.');
        string intPart = dot < 0 ? value : value[..dot];
        string decPart = dot < 0 ? "" : value[(dot + 1)..];

        var sb = new StringBuilder();
        if (negative) sb.Append("minus ");
        if (intPart.Length > 0)
        {
            sb.Append(DigitsOrNumber(intPart));
        }
        if (decPart.Length > 0)
        {
            if (intPart.Length > 0) sb.Append(' ');
            sb.Append("point");
            foreach (char c in decPart) sb.Append(' ').Append(Ones[c - '0']);
        }
        return sb.ToString();
    }

    private static string Pluralize(string words)
    {
        int split = Math.Max(words.LastIndexOf(' '), words.LastIndexOf('-'));
        string prefix = split >= 0 ? words[..(split + 1)] : "";
        string last = split >= 0 ? words[(split + 1)..] : words;
        if (last.EndsWith('y')) return prefix + last[..^1] + "ies";
        if (last == "six") return prefix + "sixes";
        return prefix + last + "s";
    }

    public static string NumberToWords(long number)
    {
        if (number == 0) return "zero";

        if (number < 0)
        {
            return "minus " + NumberToWords(Math.Abs(number));
        }

        var sb = new StringBuilder();

        (long Value, string Name)[] scales =
        {
            (1_000_000_000_000, "trillion"), (1_000_000_000, "billion"), (1_000_000, "million"), (1_000, "thousand")
        };
        foreach (var (value, name) in scales)
        {
            if (number >= value)
            {
                sb.Append(NumberToWords(number / value)).Append(' ').Append(name);
                number %= value;
                if (number > 0) sb.Append(' ');
            }
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
        if (number <= 0) return number.ToString(CultureInfo.InvariantCulture);

        string words = NumberToWords(number);

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

        if (year % 1000 == 0)
        {
            return NumberToWords(year);
        }

        if (year % 100 == 0)
        {
            return NumberToWords(year / 100) + " hundred";
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
