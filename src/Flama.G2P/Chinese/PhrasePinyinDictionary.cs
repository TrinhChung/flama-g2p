// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;

namespace Flama.G2P.Chinese;

public class PhrasePinyinDictionary
{
    private readonly Dictionary<string, string[]> _phrases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "银行", new[] { "yin2", "hang2" } },
        { "行走", new[] { "xing2", "zou3" } },
        { "音乐", new[] { "yin1", "yue4" } },
        { "快乐", new[] { "kuai4", "le4" } },
        { "重庆", new[] { "chong2", "qing4" } },
        { "重要", new[] { "zhong4", "yao4" } }
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
}
