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
using Flama.G2P.English;

namespace Flama.G2P.Chinese;

public class ChineseG2P : IG2P
{
    private readonly PinyinDictionary _pinyinDict;
    private readonly PhrasePinyinDictionary _phraseDict;
    private readonly IG2P _englishG2p;

    public ChineseG2P(IG2P? englishG2p = null)
    {
        _pinyinDict = new PinyinDictionary();
        _phraseDict = new PhrasePinyinDictionary();
        _englishG2p = englishG2p ?? new EnglishG2P();
    }

    public string Phonemize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Normalize numbers
        text = NormalizeNumbers(text);

        // 2. Map punctuation
        text = MapPunctuation(text);

        // 3. Segment and phonemize
        var matches = Regex.Matches(text, @"[\u4E00-\u9FFF]+|[^\u4E00-\u9FFF]+");
        var result = new StringBuilder();

        foreach (Match match in matches)
        {
            string segment = match.Value;
            if (string.IsNullOrEmpty(segment)) continue;

            if (Regex.IsMatch(segment, @"^[\u4E00-\u9FFF]+$"))
            {
                result.Append(PhonemizeChineseSegment(segment));
            }
            else
            {
                result.Append(PhonemizeNonChineseSegment(segment));
            }
        }

        return CleanOutput(result.ToString());
    }

    private string PhonemizeChineseSegment(string segment)
    {
        // 1. Segment using longest phrase dictionary matching, falling back to single characters
        List<string> pinyins = _phraseDict.SegmentAndGetPinyin(segment, _pinyinDict);

        // 2. Apply adjacent third-tone sandhi rules
        ApplyThirdToneSandhi(pinyins);

        // 3. Apply exact-match tone sandhi for 'yi' and 'bu'
        ApplyYiBuSandhi(pinyins);

        // 4. Convert Pinyin to IPA and apply retone arrows
        var ipaParts = new List<string>();
        foreach (string py in pinyins)
        {
            if (py.Length > 0 && !char.IsLetter(py[0]) && py[0] != 'ü')
            {
                continue;
            }

            string ipa = PinyinToIpa.Convert(py);
            ipa = Retone(ipa);
            if (!string.IsNullOrEmpty(ipa))
            {
                ipaParts.Add(ipa);
            }
        }

        return " " + string.Join(" ", ipaParts) + " ";
    }

    /// <summary>
    /// Applies third-tone sandhi: when two third-tone syllables are adjacent,
    /// the first changes to a second tone.
    /// Note: This is a local adjacent-pair sandhi rule. It does not perform full prosodic word/phrase
    /// segmentation, so complex multi-word sequences may sometimes differ from actual spoken rhythm.
    /// </summary>
    private static void ApplyThirdToneSandhi(List<string> pinyins)
    {
        for (int i = 0; i < pinyins.Count - 1; i++)
        {
            if (pinyins[i].EndsWith("3") && pinyins[i + 1].EndsWith("3"))
            {
                pinyins[i] = pinyins[i][..^1] + "2";
            }
        }
    }

    /// <summary>
    /// Applies tone sandhi for "bu4" and "yi1" depending on the tone of the subsequent syllable.
    /// </summary>
    private static void ApplyYiBuSandhi(List<string> pinyins)
    {
        for (int i = 0; i < pinyins.Count - 1; i++)
        {
            string current = pinyins[i];
            string next = pinyins[i + 1];

            if (next.Length > 1 && char.IsDigit(next[^1]))
            {
                char nextTone = next[^1];

                if (current == "bu4")
                {
                    if (nextTone == '4')
                    {
                        pinyins[i] = "bu2";
                    }
                }
                else if (current == "yi1")
                {
                    if (nextTone == '4')
                    {
                        pinyins[i] = "yi2";
                    }
                    else if (nextTone == '1' || nextTone == '2' || nextTone == '3')
                    {
                        pinyins[i] = "yi4";
                    }
                }
            }
        }
    }

    private string PhonemizeNonChineseSegment(string segment)
    {
        var tokens = Regex.Matches(segment, @"[A-Za-z']+|[^\sA-Za-z']+|\s+");
        var sb = new StringBuilder();

        foreach (Match match in tokens)
        {
            string token = match.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                sb.Append(' ');
                continue;
            }

            if (Regex.IsMatch(token, @"^[A-Za-z']+$"))
            {
                string enIpa = _englishG2p.Phonemize(token);
                sb.Append(enIpa);
            }
            else
            {
                sb.Append(token);
            }
        }

        return sb.ToString();
    }

    private static string Retone(string p)
    {
        p = p.Replace("˧˩˧", "↓"); // third tone
        p = p.Replace("˧˥", "↗");  // second tone
        p = p.Replace("˥˩", "↘");  // fourth tone
        p = p.Replace("˥", "→");   // first tone
        
        p = p.Replace("ɻ\u0329", "ɨ").Replace("ɹ\u0329", "ɨ");
        p = p.Replace("ɻ̩", "ɨ").Replace("ɹ̩", "ɨ");
        p = p.Replace("m0", "m").Replace("n0", "n").Replace("ŋ0", "ŋ");
        return p;
    }

    private static string MapPunctuation(string text)
    {
        text = text.Replace("、", ", ").Replace("，", ", ");
        text = text.Replace("。", ". ").Replace("．", ". ");
        text = text.Replace("！", "! ");
        text = text.Replace("：", ": ");
        text = text.Replace("；", "; ");
        text = text.Replace("？", "? ");
        text = text.Replace("«", " “").Replace("»", "” ");
        text = text.Replace("《", " “").Replace("》", "” ");
        text = text.Replace("「", " “").Replace("」", "” ");
        text = text.Replace("『", " “").Replace("』", "” ");
        text = text.Replace("（", " (").Replace("）", ") ");
        return text.Trim();
    }

    public static string NormalizeNumbers(string text)
    {
        return Regex.Replace(text, @"\d+", m =>
        {
            if (long.TryParse(m.Value, out long val))
            {
                return IntegerToChinese(val);
            }
            return m.Value;
        });
    }

    private static readonly string[] ChineseDigits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] ChineseUnits = { "", "十", "百", "千" };
    private static readonly string[] ChineseBigUnits = { "", "万", "亿", "万亿" };

    public static string IntegerToChinese(long number)
    {
        if (number == 0) return ChineseDigits[0];
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
                    result = ChineseDigits[0] + result;
                    needZero = false;
                }
                string unit = bigUnitIndex < ChineseBigUnits.Length ? ChineseBigUnits[bigUnitIndex] : "";
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
                    result = ChineseDigits[0] + result;
                    lastWasZero = false;
                }
                string unit = unitIndex < ChineseUnits.Length ? ChineseUnits[unitIndex] : "";
                result = ChineseDigits[digit] + unit + result;
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

    private static string CleanOutput(string input)
    {
        string cleaned = Regex.Replace(input, @"\s+", " ");
        return cleaned.Trim();
    }
}
