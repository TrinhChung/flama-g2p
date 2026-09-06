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
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

public class CmuDictionary
{
    private readonly Dictionary<string, string[]> _dict = new(StringComparer.OrdinalIgnoreCase);

    public CmuDictionary(string? dictFilePath = null)
    {
        LoadBuiltinDefaults();

        if (!string.IsNullOrEmpty(dictFilePath) && File.Exists(dictFilePath))
        {
            LoadFromFile(dictFilePath);
        }
    }

    public bool TryLookup(string word, out string[] arpabetTokens)
    {
        string cleaned = CleanWord(word);
        return _dict.TryGetValue(cleaned, out arpabetTokens!);
    }

    public void LoadFromFile(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";;;")) continue;

            // CMU dict formats use either double-space, single-space, or tab as separator
            // Split on first whitespace run to separate word from phonemes
            var parts = Regex.Split(line.Trim(), @"\s{2,}|\t");
            if (parts.Length < 2)
            {
                // Fallback: split on first space
                int spaceIdx = line.IndexOf(' ');
                if (spaceIdx <= 0) continue;
                parts = new[] { line[..spaceIdx], line[(spaceIdx + 1)..].Trim() };
            }

            string rawWord = parts[0];
            // Remove alternate pronunciation suffix like WORD(2)
            string word = Regex.Replace(rawWord, @"\(\d+\)$", "").Trim().ToUpperInvariant();
            
            string[] phonemes = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Keep first/primary entry if not present
            _dict.TryAdd(word, phonemes);
        }
    }

    private static string CleanWord(string word)
    {
        return Regex.Replace(word.Trim().ToUpperInvariant(), @"[^A-Z']", "");
    }

    private void LoadBuiltinDefaults()
    {
        var defaults = new Dictionary<string, string[]>
        {
            { "HELLO", new[] { "HH", "AH0", "L", "OW1" } },
            { "WORLD", new[] { "W", "ER1", "L", "D" } },
            { "THIS", new[] { "DH", "IH1", "S" } },
            { "IS", new[] { "IH1", "Z" } },
            { "A", new[] { "AH0" } },
            { "AN", new[] { "AE1", "N" } },
            { "TEST", new[] { "T", "EH1", "S", "T" } },
            { "OF", new[] { "AH1", "V" } },
            { "KOKORO", new[] { "K", "OW1", "K", "ER0", "OW0" } },
            { "TEXT", new[] { "T", "EH1", "K", "S", "T" } },
            { "TO", new[] { "T", "UW1" } },
            { "SPEECH", new[] { "S", "P", "IY1", "CH" } },
            { "ENGINE", new[] { "EH1", "N", "JH", "AH0", "N" } },
            { "ENGLISH", new[] { "IH1", "NG", "G", "L", "IH0", "SH" } },
            { "JAPANESE", new[] { "JH", "AE1", "P", "AH0", "N", "IY1", "Z" } },
            { "FLAMA", new[] { "F", "L", "AA1", "M", "AH0" } },
            { "AUDIO", new[] { "AA1", "D", "IY0", "OW0" } },
            { "GOOD", new[] { "G", "UH1", "D" } },
            { "MORNING", new[] { "M", "AO1", "R", "N", "IH0", "NG" } },
            { "AFTERNOON", new[] { "AE2", "F", "T", "ER0", "N", "UW1", "N" } },
            { "EVENING", new[] { "IY1", "V", "N", "IH0", "NG" } },
            { "THE", new[] { "DH", "AH0" } },
            { "YOU", new[] { "Y", "UW1" } },
            { "ARE", new[] { "AA1", "R" } },
            { "HOW", new[] { "HH", "AW1" } },
            { "WELCOME", new[] { "W", "EH1", "L", "K", "AH0", "M" } },

            // Numbers
            { "ZERO", new[] { "Z", "IH1", "R", "OW0" } },
            { "ONE", new[] { "W", "AH1", "N" } },
            { "TWO", new[] { "T", "UW1" } },
            { "THREE", new[] { "TH", "R", "IY1" } },
            { "FOUR", new[] { "F", "AO1", "R" } },
            { "FIVE", new[] { "F", "AY1", "V" } },
            { "SIX", new[] { "S", "IH1", "K", "S" } },
            { "SEVEN", new[] { "S", "EH1", "V", "AH0", "N" } },
            { "EIGHT", new[] { "EY1", "T" } },
            { "NINE", new[] { "N", "AY1", "N" } },
            { "TEN", new[] { "T", "EH1", "N" } },
            { "ELEVEN", new[] { "IH0", "L", "EH1", "V", "AH0", "N" } },
            { "TWELVE", new[] { "T", "W", "EH1", "L", "V" } },
            { "THIRTEEN", new[] { "TH", "ER0", "T", "IY1", "N" } },
            { "FOURTEEN", new[] { "F", "AO0", "R", "T", "IY1", "N" } },
            { "FIFTEEN", new[] { "F", "IH0", "F", "T", "IY1", "N" } },
            { "SIXTEEN", new[] { "S", "IH0", "K", "S", "T", "IY1", "N" } },
            { "SEVENTEEN", new[] { "S", "EH2", "V", "AH0", "N", "T", "IY1", "N" } },
            { "EIGHTEEN", new[] { "EY0", "T", "IY1", "N" } },
            { "NINETEEN", new[] { "N", "AY0", "N", "T", "IY1", "N" } },
            { "TWENTY", new[] { "T", "W", "EH1", "N", "T", "IY0" } },
            { "THIRTY", new[] { "TH", "ER1", "T", "IY0" } },
            { "FORTY", new[] { "F", "AO1", "R", "T", "IY0" } },
            { "FIFTY", new[] { "F", "IH1", "F", "T", "IY0" } },
            { "SIXTY", new[] { "S", "IH1", "K", "S", "T", "IY0" } },
            { "SEVENTY", new[] { "S", "EH1", "V", "AH0", "N", "T", "IY0" } },
            { "EIGHTY", new[] { "EY1", "T", "IY0" } },
            { "NINETY", new[] { "N", "AY1", "N", "T", "IY0" } },
            { "HUNDRED", new[] { "HH", "AH1", "N", "D", "R", "AH0", "D" } },
            { "THOUSAND", new[] { "TH", "AW1", "Z", "AH0", "N", "D" } },
            { "MILLION", new[] { "M", "IH1", "L", "Y", "AH0", "N" } },
            { "BILLION", new[] { "B", "IH1", "L", "Y", "AH0", "N" } },
            { "OH", new[] { "OW1" } },

            // Ordinals
            { "FIRST", new[] { "F", "ER1", "S", "T" } },
            { "SECOND", new[] { "S", "EH1", "K", "AH0", "N", "D" } },
            { "THIRD", new[] { "TH", "ER1", "D" } },
            { "FOURTH", new[] { "F", "AO1", "R", "TH" } },
            { "FIFTH", new[] { "F", "IH1", "F", "TH" } },
            { "SIXTH", new[] { "S", "IH1", "K", "S", "TH" } },
            { "SEVENTH", new[] { "S", "EH1", "V", "AH0", "N", "TH" } },
            { "EIGHTH", new[] { "EY1", "T", "TH" } },
            { "NINTH", new[] { "N", "AY1", "N", "TH" } },
            { "TENTH", new[] { "T", "EH1", "N", "TH" } },

            // Math, symbols, currencies
            { "POINT", new[] { "P", "OY1", "N", "T" } },
            { "MINUS", new[] { "M", "AY1", "N", "AH0", "S" } },
            { "PLUS", new[] { "P", "L", "AH1", "S" } },
            { "PERCENT", new[] { "P", "ER0", "S", "EH1", "N", "T" } },
            { "AND", new[] { "AH0", "N", "D" } },
            { "AT", new[] { "AE1", "T" } },
            { "SLASH", new[] { "S", "L", "AE1", "SH" } },
            { "DOLLAR", new[] { "D", "AA1", "L", "ER0" } },
            { "DOLLARS", new[] { "D", "AA1", "L", "ER0", "Z" } },
            { "CENT", new[] { "S", "EH1", "N", "T" } },
            { "CENTS", new[] { "S", "EH1", "N", "T", "S" } },
            { "EURO", new[] { "Y", "UH1", "R", "OW0" } },
            { "EUROS", new[] { "Y", "UH1", "R", "OW0", "Z" } },
            { "POUND", new[] { "P", "AW1", "N", "D" } },
            { "POUNDS", new[] { "P", "AW1", "N", "D", "Z" } },
            { "YEN", new[] { "Y", "EH1", "N" } },

            // Common conversational words
            { "I", new[] { "AY1" } },
            { "MY", new[] { "M", "AY1" } },
            { "ME", new[] { "M", "IY1" } },
            { "WE", new[] { "W", "IY1" } },
            { "OUR", new[] { "AW1", "ER0" } },
            { "US", new[] { "AH1", "S" } },
            { "THEY", new[] { "DH", "EY1" } },
            { "THEIR", new[] { "DH", "EH1", "R" } },
            { "THEM", new[] { "DH", "EH1", "M" } },
            { "HE", new[] { "HH", "IY1" } },
            { "SHE", new[] { "SH", "IY1" } },
            { "IT", new[] { "IH1", "T" } },
            { "HAVE", new[] { "HH", "AE1", "V" } },
            { "HAS", new[] { "HH", "AE1", "Z" } },
            { "HAD", new[] { "HH", "AE1", "D" } },
            { "DO", new[] { "D", "UW1" } },
            { "CAN", new[] { "K", "AE1", "N" } },
            { "WILL", new[] { "W", "IH1", "L" } },
            { "WHAT", new[] { "W", "AH1", "T" } },
            { "FOR", new[] { "F", "AO1", "R" } },
            { "WITH", new[] { "W", "IH1", "DH" } },
            { "IN", new[] { "IH1", "N" } },
            { "ON", new[] { "AA1", "N" } },
            { "BOOK", new[] { "B", "UH1", "K" } },
            { "APPLE", new[] { "AE1", "P", "AH0", "L" } },
            { "CAR", new[] { "K", "AA1", "R" } },
            { "NAME", new[] { "N", "EY1", "M" } },
            { "THANK", new[] { "TH", "AE1", "NG", "K" } },
            { "THANKS", new[] { "TH", "AE1", "NG", "K", "S" } }
        };

        foreach (var (k, v) in defaults)
        {
            _dict[k] = v;
        }
    }
}
