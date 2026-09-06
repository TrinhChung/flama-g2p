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
using System.Linq;
using System.Text;

namespace Flama.G2P.Chinese;

public class PhrasePinyinDictionary
{
    private readonly Dictionary<string, string[]> _phrases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common polyphones & established phrases
        { "银行", new[] { "yin2", "hang2" } },
        { "行走", new[] { "xing2", "zou3" } },
        { "音乐", new[] { "yin1", "yue4" } },
        { "快乐", new[] { "kuai4", "le4" } },
        { "重庆", new[] { "chong2", "qing4" } },
        { "重要", new[] { "zhong4", "yao4" } },
        { "教育", new[] { "jiao4", "yu4" } },
        { "假话", new[] { "jia3", "hua4" } },
        { "假装", new[] { "jia3", "zhuang1" } },
        { "假使", new[] { "jia3", "shi3" } },
        { "假如", new[] { "jia3", "ru2" } },
        { "放假", new[] { "fang4", "jia4" } },
        { "假期", new[] { "jia4", "qi1" } },
        { "长假", new[] { "chang2", "jia4" } },

        // Potential complements (V + 不 + C -> bu5)
        { "得不到", new[] { "de5", "bu5", "dao4" } },
        { "看不懂", new[] { "kan4", "bu5", "dong3" } },
        { "想不到", new[] { "xiang3", "bu5", "dao4" } },
        { "找不着", new[] { "zhao3", "bu5", "zhao2" } },
        { "做不好", new[] { "zuo4", "bu5", "hao3" } },
        { "听不见", new[] { "ting1", "bu5", "jian4" } },
        { "拿不起", new[] { "na2", "bu5", "qi3" } },
        { "吃不下", new[] { "chi1", "bu5", "xia4" } },
        { "去不了", new[] { "qu4", "bu5", "liao3" } },

        // Essential neutral tone words (must_neural_tone_words from misaki)
        { "东西", new[] { "dong1", "xi5" } },
        { "时候", new[] { "shi2", "hou5" } },
        { "清楚", new[] { "qing1", "chu5" } },
        { "事情", new[] { "shi4", "qing5" } },
        { "孩子", new[] { "hai2", "zi5" } },
        { "朋友", new[] { "peng2", "you5" } },
        { "喜欢", new[] { "xi3", "huan5" } },
        { "明白", new[] { "ming2", "bai5" } },
        { "认识", new[] { "ren4", "shi5" } },
        { "告诉", new[] { "gao4", "su5" } },
        { "照顾", new[] { "zhao4", "gu5" } },
        { "客气", new[] { "ke4", "qi5" } },
        { "便宜", new[] { "pian2", "yi5" } },
        { "暖和", new[] { "nuan3", "huo5" } },
        { "舒服", new[] { "shu1", "fu5" } },
        { "精神", new[] { "jing1", "shen5" } },
        { "名字", new[] { "ming2", "zi5" } },
        { "地方", new[] { "di4", "fang5" } },
        { "打算", new[] { "da3", "suan5" } },
        { "打听", new[] { "da3", "ting5" } },
        { "意思", new[] { "yi4", "si5" } },
        { "麻烦", new[] { "ma2", "fan5" } },
        { "聪明", new[] { "cong1", "ming5" } },
        { "耳朵", new[] { "er3", "duo5" } },
        { "头发", new[] { "tou2", "fa5" } },
        { "眼睛", new[] { "yan3", "jing5" } },
        { "嘴巴", new[] { "zui3", "ba5" } },
        { "窗户", new[] { "chuang1", "hu5" } },
        { "怎么", new[] { "zen3", "me5" } },
        { "什么", new[] { "shen2", "me5" } },
        { "这么", new[] { "zhe4", "me5" } },
        { "那么", new[] { "na4", "me5" } },
        { "我们", new[] { "wo3", "men5" } },
        { "你们", new[] { "ni3", "men5" } },
        { "他们", new[] { "ta1", "men5" } },
        { "她们", new[] { "ta1", "men5" } },
        { "它们", new[] { "ta1", "men5" } },
        { "人们", new[] { "ren2", "men5" } },
        { "桌子", new[] { "zhuo1", "zi5" } },
        { "椅子", new[] { "yi3", "zi5" } },
        { "本子", new[] { "ben3", "zi5" } },
        { "屋子", new[] { "wu1", "zi5" } },
        { "儿子", new[] { "er2", "zi5" } },
        { "孙子", new[] { "sun1", "zi5" } },
        { "裤子", new[] { "ku4", "zi5" } },
        { "鞋子", new[] { "xie2", "zi5" } },
        { "杯子", new[] { "bei1", "zi5" } },
        { "瓶子", new[] { "ping2", "zi5" } },

        // Family reduplications
        { "爸爸", new[] { "ba4", "ba5" } },
        { "妈妈", new[] { "ma1", "ma5" } },
        { "爷爷", new[] { "ye2", "ye5" } },
        { "奶奶", new[] { "nai3", "nai5" } },
        { "哥哥", new[] { "ge1", "ge5" } },
        { "姐姐", new[] { "jie3", "jie5" } },
        { "弟弟", new[] { "di4", "di5" } },
        { "妹妹", new[] { "mei4", "mei5" } },
        { "叔叔", new[] { "shu1", "shu5" } },
        { "姑姑", new[] { "gu1", "gu5" } },
        { "舅舅", new[] { "jiu4", "jiu5" } },
        { "太太", new[] { "tai4", "tai5" } },

        // Directional complement suffixes
        { "起来", new[] { "qi3", "lai5" } },
        { "出来", new[] { "chu1", "lai5" } },
        { "进来", new[] { "jin4", "lai5" } },
        { "上来", new[] { "shang4", "lai5" } },
        { "下来", new[] { "xia4", "lai5" } },
        { "过来", new[] { "guo4", "lai5" } },
        { "回去", new[] { "hui2", "qu5" } },
        { "过去", new[] { "guo4", "qu5" } },
        { "进去", new[] { "jin4", "qu5" } },
        { "出去", new[] { "chu1", "qu5" } },

        // Common compound phrases in text
        { "弄清楚", new[] { "nong4", "qing1", "chu5" } },
        { "聊天儿", new[] { "liao2", "tian1", "er5" } },
        { "聊聊天", new[] { "liao2", "liao5", "tian1" } },
        { "聊聊天儿", new[] { "liao2", "liao5", "tian1", "er5" } },
        { "整理整理", new[] { "zheng3", "li3", "zheng3", "li5" } },
        { "合适", new[] { "he2", "shi4" } },
        { "解决", new[] { "jie3", "jue2" } },
        { "选择", new[] { "xuan3", "ze2" } }
    };

    public PhrasePinyinDictionary()
    {
    }

    /// <summary>
    /// Registers a custom phrase to Pinyin mapping. 
    /// This makes the phrase lexicon extensible.
    /// </summary>
    public void AddPhrase(string phrase, string[] pinyins)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            throw new ArgumentException("Phrase cannot be empty.", nameof(phrase));
        }
        if (pinyins == null || pinyins.Length != phrase.Length)
        {
            throw new ArgumentException("Pinyin array length must match the phrase character count.", nameof(pinyins));
        }
        _phrases[phrase] = pinyins;
    }

    private static readonly Dictionary<char, (char Replacement, int Tone)> DiacriticMap = new()
    {
        { 'ā', ('a', 1) }, { 'á', ('a', 2) }, { 'ǎ', ('a', 3) }, { 'à', ('a', 4) },
        { 'ē', ('e', 1) }, { 'é', ('e', 2) }, { 'ě', ('e', 3) }, { 'è', ('e', 4) },
        { 'ī', ('i', 1) }, { 'í', ('i', 2) }, { 'ǐ', ('i', 3) }, { 'ì', ('i', 4) },
        { 'ō', ('o', 1) }, { 'ó', ('o', 2) }, { 'ǒ', ('o', 3) }, { 'ò', ('o', 4) },
        { 'ū', ('u', 1) }, { 'ú', ('u', 2) }, { 'ǔ', ('u', 3) }, { 'ù', ('u', 4) },
        { 'ǖ', ('v', 1) }, { 'ǘ', ('v', 2) }, { 'ǚ', ('v', 3) }, { 'ǜ', ('v', 4) },
        { 'ü', ('v', 5) },
        { 'ń', ('n', 2) }, { 'ň', ('n', 3) }, { 'ǹ', ('n', 4) },
        { 'ḿ', ('m', 2) }
    };

    public static string NormalizePinyinWithToneNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        raw = raw.Trim().ToLowerInvariant();
        if (char.IsDigit(raw[^1])) return raw;

        int tone = 5;
        var sb = new StringBuilder();
        foreach (char c in raw)
        {
            if (DiacriticMap.TryGetValue(c, out var mapped))
            {
                sb.Append(mapped.Replacement);
                tone = mapped.Tone;
            }
            else
            {
                sb.Append(c);
            }
        }
        sb.Append(tone);
        return sb.ToString();
    }

    /// <summary>
    /// Loads an external large phrase lexicon file into memory.
    /// Supports TSV format (phrase \t py1 py2 ...), colon format (phrase: py1 py2 or phrase: py1,py2),
    /// and diacritic tones (e.g. yī -> yi1).
    /// </summary>
    public int LoadExternalLexicon(string filePath)
    {
        if (!File.Exists(filePath)) return 0;

        int loadedCount = 0;
        foreach (string line in File.ReadLines(filePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            string phrase;
            string[] pinyins;

            if (line.Contains('\t'))
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                phrase = parts[0].Trim();
                pinyins = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            else if (line.Contains(':'))
            {
                var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                phrase = parts[0].Trim();
                pinyins = parts[1].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                phrase = parts[0].Trim();
                pinyins = parts.Skip(1).ToArray();
            }

            if (phrase.Length > 0 && pinyins.Length == phrase.Length)
            {
                for (int k = 0; k < pinyins.Length; k++)
                {
                    pinyins[k] = NormalizePinyinWithToneNumber(pinyins[k]);
                }
                _phrases[phrase] = pinyins;
                loadedCount++;
            }
        }

        return loadedCount;
    }

    public int Count => _phrases.Count;

    /// <summary>
    /// Processes Chinese text segments using longest-phrase match resolution,
    /// falling back to the single character dictionary.
    /// </summary>
    public List<string> SegmentAndGetPinyin(string text, PinyinDictionary singleCharDict)
    {
        var result = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            string? matchedPhrase = null;
            string[]? matchedPinyins = null;
            int maxLen = 0;

            foreach (var kvp in _phrases)
            {
                string phrase = kvp.Key;
                if (i + phrase.Length <= text.Length)
                {
                    bool match = true;
                    for (int k = 0; k < phrase.Length; k++)
                    {
                        if (text[i + k] != phrase[k])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match && phrase.Length > maxLen)
                    {
                        maxLen = phrase.Length;
                        matchedPhrase = phrase;
                        matchedPinyins = kvp.Value;
                    }
                }
            }

            if (matchedPhrase != null && matchedPinyins != null)
            {
                result.AddRange(matchedPinyins);
                i += maxLen;
            }
            else
            {
                char c = text[i];
                string? py = singleCharDict.GetPinyin(c);
                if (py != null)
                {
                    result.Add(py);
                }
                else
                {
                    result.Add(c.ToString());
                }
                i++;
            }
        }
        return result;
    }

    /// <summary>
    /// Processes Chinese text segments using longest-phrase match resolution,
    /// grouping syllables into words (like misaki's word-level tokenization).
    /// </summary>
    public List<List<string>> SegmentAndGetWordPinyin(string text, PinyinDictionary singleCharDict)
    {
        var result = new List<List<string>>();
        int i = 0;
        while (i < text.Length)
        {
            string? matchedPhrase = null;
            string[]? matchedPinyins = null;
            int maxLen = 0;

            foreach (var kvp in _phrases)
            {
                string phrase = kvp.Key;
                if (i + phrase.Length <= text.Length)
                {
                    bool match = true;
                    for (int k = 0; k < phrase.Length; k++)
                    {
                        if (text[i + k] != phrase[k])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match && phrase.Length > maxLen)
                    {
                        maxLen = phrase.Length;
                        matchedPhrase = phrase;
                        matchedPinyins = kvp.Value;
                    }
                }
            }

            if (matchedPhrase != null && matchedPinyins != null)
            {
                result.Add(new List<string>(matchedPinyins));
                i += maxLen;
            }
            else
            {
                // Check if next character is identical (reduplication like 看看, 听听, 聊聊)
                if (i + 1 < text.Length && text[i] == text[i + 1])
                {
                    char c = text[i];
                    string? py = singleCharDict.GetPinyin(c);
                    if (py != null)
                    {
                        // Reduplication: second syllable becomes neutral tone 5
                        string py1 = py;
                        string py2 = py.Length > 1 && char.IsDigit(py[^1]) ? py[..^1] + "5" : py + "5";
                        result.Add(new List<string> { py1, py2 });
                    }
                    else
                    {
                        result.Add(new List<string> { c.ToString(), text[i + 1].ToString() });
                    }
                    i += 2;
                }
                else
                {
                    char c = text[i];
                    string? py = singleCharDict.GetPinyin(c);
                    if (py != null)
                    {
                        result.Add(new List<string> { py });
                    }
                    else
                    {
                        result.Add(new List<string> { c.ToString() });
                    }
                    i++;
                }
            }
        }
        return result;
    }
}
