// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly ToneSandhi _toneSandhi;

    public bool IsHighPrecisionMode { get; private set; }
    public int LexiconWordCount => _phraseDict.Count;

    public ChineseG2P(IG2P? englishG2p = null, string? customDataDir = null)
    {
        _pinyinDict = new PinyinDictionary();
        _phraseDict = new PhrasePinyinDictionary();
        _englishG2p = englishG2p ?? new EnglishG2P();
        _toneSandhi = new ToneSandhi();

        InitializeLexicon(customDataDir);
    }

    public void InitializeLexicon(string? customDataDir = null)
    {
        string dataDir = customDataDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flama", "Audio", "data", "zh");

        string largePinyinTsv = Path.Combine(dataDir, "large_pinyin.txt");
        if (File.Exists(largePinyinTsv))
        {
            int loaded = _phraseDict.LoadExternalLexicon(largePinyinTsv);
            IsHighPrecisionMode = loaded > 0;
        }
        else
        {
            IsHighPrecisionMode = false;
        }
    }

    public string Phonemize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Comprehensive Text Normalization (Decimals, Dates, Times, Percentages, Quantifiers, Integers)
        text = ChineseTextNormalizer.Normalize(text);

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
                if (result.Length > 0 && result[^1] != ' ')
                {
                    result.Append(' ');
                }
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
        // 1. Segment using phrase dictionary matching into word groups
        List<List<string>> wordGroups = _phraseDict.SegmentAndGetWordPinyin(segment, _pinyinDict);

        // 2. Apply word-level neural tones
        int charIdx = 0;
        foreach (var group in wordGroups)
        {
            int len = group.Count;
            if (charIdx + len <= segment.Length)
            {
                string word = segment.Substring(charIdx, len);
                _toneSandhi.ApplyNeuralTone(word, group);
            }
            charIdx += len;
        }

        // 3. Flatten pinyins to apply Yi, Bu, and 3rd-tone sandhi across adjacent syllables
        var flatPinyins = new List<string>();
        foreach (var group in wordGroups)
        {
            flatPinyins.AddRange(group);
        }

        _toneSandhi.ApplyBuSandhi(segment, flatPinyins);
        _toneSandhi.ApplyYiSandhi(segment, flatPinyins);
        _toneSandhi.ApplyThirdToneSandhi(flatPinyins);

        // 4. Reconstruct word groups with updated tone sandhi and convert to IPA
        int flatIdx = 0;
        var wordIpas = new List<string>();

        foreach (var group in wordGroups)
        {
            var sbWord = new StringBuilder();
            for (int k = 0; k < group.Count; k++)
            {
                string py = flatPinyins[flatIdx++];
                if (py.Length > 0 && !char.IsLetter(py[0]) && py[0] != 'ü')
                {
                    continue;
                }

                string ipa = PinyinToIpa.Convert(py);
                ipa = Retone(ipa);
                sbWord.Append(ipa);
            }

            if (sbWord.Length > 0)
            {
                wordIpas.Add(sbWord.ToString());
            }
        }

        return string.Join(" ", wordIpas);
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
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Remove whitespace before punctuation marks: , . ! ? : ; ” )
        string cleaned = Regex.Replace(input, @"\s+([,.:;!?\)""”])", "$1");

        // 2. Remove whitespace after opening punctuation marks: “ (
        cleaned = Regex.Replace(cleaned, @"([\(“""])\s+", "$1");

        // 3. Ensure single space after punctuation if followed by a non-punctuation char
        cleaned = Regex.Replace(cleaned, @"([,.:;!?])([^\s,.:;!?])", "$1 $2");

        // 4. Collapse multiple whitespace into a single space
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }
}
