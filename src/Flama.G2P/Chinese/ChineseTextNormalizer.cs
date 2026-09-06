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

namespace Flama.G2P.Chinese;

/// <summary>
/// Text Normalizer for Mandarin Chinese G2P (adapted from PaddleSpeech and Misaki zh_normalization).
/// Converts non-standard words (NSW) such as dates, times, decimals, percentages,
/// fractions, phone numbers, and quantifiers into verbalized Chinese characters.
/// </summary>
public static class ChineseTextNormalizer
{
    private static readonly string[] Digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] Units = { "", "十", "百", "千" };
    private static readonly string[] BigUnits = { "", "万", "亿", "万亿" };

    // Common measure words where "2" must be verbalized as "两" instead of "二"
    private static readonly HashSet<char> MeasureWords = new()
    {
        '个', '只', '条', '本', '支', '张', '块', '把', '位', '瓶', '杯',
        '双', '套', '家', '部', '间', '天', '年', '次', '点', '份', '栋',
        '首', '门', '句', '辆', '台', '架', '艘', '匹', '座', '名', '斤', '两'
    };

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Convert Full-width ASCII & digits to Half-width
        text = ToHalfWidth(text);

        // 2. Units, symbols and temperatures
        text = NormalizeSymbols(text);

        // 3. Dates (e.g. 2026年9月6日, 2026-09-06, 2026/09/06)
        text = NormalizeDates(text);

        // 4. Time (e.g. 15:30, 08:05:20)
        text = NormalizeTimes(text);

        // 5. Mobile & Telephone numbers (11 digits: 13800138000)
        text = NormalizePhoneNumbers(text);

        // 6. Percentages (e.g. 25%, 3.5%)
        text = NormalizePercentages(text);

        // 7. Fractions (e.g. 1/3, 2/5)
        text = NormalizeFractions(text);

        // 8. Decimals (e.g. 3.14, 0.05)
        text = NormalizeDecimals(text);

        // 9. Quantifier "2" before measure words (e.g. 2个 -> 两个)
        text = NormalizeQuantifiers(text);

        // 10. Remaining pure integers
        text = NormalizeIntegers(text);

        // 11. Traditional to Simplified common character conversion
        text = ConvertTraditionalToSimplified(text);

        return text;
    }

    private static string ToHalfWidth(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c == 0x3000) // Full-width space
            {
                sb.Append(' ');
            }
            else if (c >= 0xFF01 && c <= 0xFF5E) // Full-width ASCII
            {
                sb.Append((char)(c - 0xFEE0));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string NormalizeSymbols(string text)
    {
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*℃", m => $"{m.Groups[1].Value}摄氏度");
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*°C", m => $"{m.Groups[1].Value}摄氏度", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*°", m => $"{m.Groups[1].Value}度");

        // Currencies
        text = Regex.Replace(text, @"\$\s*(\d+(?:\.\d+)?)", m => $"{m.Groups[1].Value}美元");
        text = Regex.Replace(text, @"[¥￥]\s*(\d+(?:\.\d+)?)", m => $"{m.Groups[1].Value}元");
        text = Regex.Replace(text, @"€\s*(\d+(?:\.\d+)?)", m => $"{m.Groups[1].Value}欧元");

        // Units
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*kg\b", m => $"{m.Groups[1].Value}千克", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*km\b", m => $"{m.Groups[1].Value}千米", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*cm\b", m => $"{m.Groups[1].Value}厘米", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*m\b", m => $"{m.Groups[1].Value}米", RegexOptions.IgnoreCase);

        return text;
    }

    private static string NormalizeDates(string text)
    {
        // 2026年9月6日 or 2026年09月06日
        text = Regex.Replace(text, @"(\d{2,4})年\s*(\d{1,2})月\s*(\d{1,2})[日号]", m =>
        {
            string year = DigitsToChinese(m.Groups[1].Value);
            string month = IntegerToChinese(long.Parse(m.Groups[2].Value));
            string day = IntegerToChinese(long.Parse(m.Groups[3].Value));
            return $"{year}年{month}月{day}日";
        });

        // 2026年9月
        text = Regex.Replace(text, @"(\d{2,4})年\s*(\d{1,2})月", m =>
        {
            string year = DigitsToChinese(m.Groups[1].Value);
            string month = IntegerToChinese(long.Parse(m.Groups[2].Value));
            return $"{year}年{month}月";
        });

        // 2026年
        text = Regex.Replace(text, @"(\d{2,4})年", m =>
        {
            string year = DigitsToChinese(m.Groups[1].Value);
            return $"{year}年";
        });

        // 9月6日
        text = Regex.Replace(text, @"(\d{1,2})月\s*(\d{1,2})[日号]", m =>
        {
            string month = IntegerToChinese(long.Parse(m.Groups[1].Value));
            string day = IntegerToChinese(long.Parse(m.Groups[2].Value));
            return $"{month}月{day}日";
        });

        return text;
    }

    private static string NormalizeTimes(string text)
    {
        // HH:mm:ss
        text = Regex.Replace(text, @"(?<!\d)([01]?\d|2[0-3]):([0-5]\d):([0-5]\d)(?!\d)", m =>
        {
            string h = IntegerToChinese(long.Parse(m.Groups[1].Value));
            string min = IntegerToChinese(long.Parse(m.Groups[2].Value));
            string s = IntegerToChinese(long.Parse(m.Groups[3].Value));
            return $"{h}点{min}分{s}秒";
        });

        // HH:mm
        text = Regex.Replace(text, @"(?<!\d)([01]?\d|2[0-3]):([0-5]\d)(?!\d)", m =>
        {
            int hVal = int.Parse(m.Groups[1].Value);
            int mVal = int.Parse(m.Groups[2].Value);
            string h = IntegerToChinese(hVal);
            if (mVal == 0) return $"{h}点整";
            if (mVal == 30) return $"{h}点半";
            string min = IntegerToChinese(mVal);
            return $"{h}点{min}分";
        });

        return text;
    }

    private static string NormalizePhoneNumbers(string text)
    {
        // 11-digit Chinese mobile phone starting with 1
        text = Regex.Replace(text, @"(?<!\d)1[3-9]\d{9}(?!\d)", m =>
        {
            return DigitsToChinese(m.Value);
        });

        // Hotline numbers (400, 800)
        text = Regex.Replace(text, @"(?<!\d)(400|800)(?:-?\d{3,4}){2}(?!\d)", m =>
        {
            return DigitsToChinese(m.Value.Replace("-", ""));
        });

        return text;
    }

    private static string NormalizePercentages(string text)
    {
        return Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*%", m =>
        {
            string num = m.Groups[1].Value;
            string chineseNum = num.Contains('.') ? DecimalToChinese(num) : IntegerToChinese(long.Parse(num));
            return $"百分之{chineseNum}";
        });
    }

    private static string NormalizeFractions(string text)
    {
        return Regex.Replace(text, @"(\d+)/(\d+)", m =>
        {
            string numerator = IntegerToChinese(long.Parse(m.Groups[1].Value));
            string denominator = IntegerToChinese(long.Parse(m.Groups[2].Value));
            return $"{denominator}分之{numerator}";
        });
    }

    private static string NormalizeDecimals(string text)
    {
        return Regex.Replace(text, @"(\d+)\.(\d+)", m =>
        {
            return DecimalToChinese(m.Value);
        });
    }

    private static string NormalizeQuantifiers(string text)
    {
        // Replace '2' before measure words with '两' (e.g. 2个 -> 两个, 2本 -> 两本)
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '2' || c == '二')
            {
                // Check if followed by a measure word
                if (i + 1 < text.Length && MeasureWords.Contains(text[i + 1]))
                {
                    // Ensure it's not part of a larger number (e.g. 12个, 22个)
                    bool prevIsDigit = i > 0 && (char.IsDigit(text[i - 1]) || text[i - 1] == '十' || text[i - 1] == '百');
                    if (!prevIsDigit)
                    {
                        sb.Append('两');
                        continue;
                    }
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string NormalizeIntegers(string text)
    {
        return Regex.Replace(text, @"\d+", m =>
        {
            if (long.TryParse(m.Value, out long val))
            {
                return IntegerToChinese(val);
            }
            return DigitsToChinese(m.Value);
        });
    }

    public static string DecimalToChinese(string decimalStr)
    {
        var parts = decimalStr.Split('.');
        if (parts.Length != 2) return decimalStr;

        string integerPart = long.TryParse(parts[0], out long intVal) ? IntegerToChinese(intVal) : DigitsToChinese(parts[0]);
        string fractionPart = DigitsToChinese(parts[1]);

        return $"{integerPart}点{fractionPart}";
    }

    public static string DigitsToChinese(string digits)
    {
        var sb = new StringBuilder();
        foreach (char c in digits)
        {
            if (c >= '0' && c <= '9')
            {
                sb.Append(Digits[c - '0']);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static string IntegerToChinese(long number)
    {
        if (number == 0) return Digits[0];
        if (number < 0) return "负" + IntegerToChinese(-number);

        string result = "";
        int bigUnitIndex = 0;
        bool needZero = false;

        long temp = number;
        while (temp > 0)
        {
            int section = (int)(temp % 10000);
            if (section > 0)
            {
                string sectionStr = SectionToChinese(section);
                if (needZero)
                {
                    result = Digits[0] + result;
                    needZero = false;
                }
                string unit = bigUnitIndex < BigUnits.Length ? BigUnits[bigUnitIndex] : "";
                result = sectionStr + unit + result;
            }
            else
            {
                if (result.Length > 0)
                {
                    needZero = true;
                }
            }

            if (temp >= 10000 && section < 1000 && section > 0)
            {
                needZero = true;
            }

            temp /= 10000;
            bigUnitIndex++;
        }

        if (result.StartsWith("一十"))
        {
            result = result[1..];
        }

        result = Regex.Replace(result, "零+", "零");
        if (result.EndsWith("零") && result.Length > 1)
        {
            result = result[..^1];
        }

        return result;
    }

    private static string SectionToChinese(int section)
    {
        string result = "";
        bool lastWasZero = false;
        int unitIndex = 0;
        int temp = section;

        while (temp > 0)
        {
            int digit = temp % 10;
            if (digit > 0)
            {
                if (lastWasZero)
                {
                    result = Digits[0] + result;
                    lastWasZero = false;
                }
                string unit = unitIndex < Units.Length ? Units[unitIndex] : "";
                result = Digits[digit] + unit + result;
            }
            else
            {
                if (result.Length > 0)
                {
                    lastWasZero = true;
                }
            }
            temp /= 10;
            unitIndex++;
        }

        return result;
    }

    private static readonly Dictionary<char, char> TraditionalToSimplifiedMap = new()
    {
        { '國', '国' }, { '學', '学' }, { '這', '这' }, { '們', '们' }, { '點', '点' },
        { '時', '时' }, { '東', '东' }, { '西', '西' }, { '話', '话' }, { '說', '说' },
        { '個', '个' }, { '為', '为' }, { '會', '会' }, { '過', '过' }, { '發', '发' },
        { '開', '开' }, { '電', '电' }, { '車', '车' }, { '經', '经' }, { '現', '现' },
        { '實', '实' }, { '問', '问' }, { '題', '题' }, { '樣', '样' }, { '讓', '让' },
        { '給', '给' }, { '長', '长' }, { '關', '关' }, { '門', '门' }, { '書', '书' }
    };

    private static string ConvertTraditionalToSimplified(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (TraditionalToSimplifiedMap.TryGetValue(c, out char simplified))
            {
                sb.Append(simplified);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
