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
using System.Reflection;
using System.Text;

namespace Flama.G2P.Chinese;

public class PinyinDictionary
{
    private readonly Dictionary<char, string> _dict = new();

    public PinyinDictionary()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "Flama.G2P.Chinese.pinyin_dict.txt";
        Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            var names = assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (name.EndsWith("pinyin_dict.txt", StringComparison.OrdinalIgnoreCase))
                {
                    stream = assembly.GetManifestResourceStream(name);
                    break;
                }
            }
        }

        if (stream == null)
        {
            throw new FileNotFoundException(
                $"Could not find embedded Pinyin dictionary resource. " +
                $"Tried: '{resourceName}' and generic check in manifest names.");
        }

        try
        {
            using (stream)
            {
                LoadFromStream(stream);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read the embedded Pinyin dictionary stream.", ex);
        }

        if (_dict.Count == 0)
        {
            throw new InvalidDataException("Pinyin dictionary loaded successfully but is empty.");
        }
    }

    private void LoadFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length >= 2 && parts[0].Length > 0)
            {
                char hanzi = parts[0][0];
                string pinyin = parts[1].Trim();
                _dict[hanzi] = pinyin;
            }
        }
    }

    public string? GetPinyin(char hanzi)
    {
        return _dict.TryGetValue(hanzi, out var pinyin) ? pinyin : null;
    }
}
