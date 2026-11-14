// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.
//
// Port of the out-of-vocabulary predictor in g2p_en (Kyubyong Park & Jongseok Kim,
// https://github.com/Kyubyong/g2p, Apache License 2.0). The weights embedded as
// English/Data/g2p_en_checkpoint20.npz are g2p_en's checkpoint20.npz, unmodified.
// See THIRD_PARTY_NOTICES.md.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Flama.G2P.English;

/// <summary>
/// GRU encoder-decoder trained on CMUdict that predicts ARPAbet for unseen words.
/// Greedy decoding, CPU only, a few milliseconds per word.
/// </summary>
public sealed class NeuralG2P
{
    private static readonly Lazy<NeuralG2P> Shared = new(() => new NeuralG2P());

    public static NeuralG2P Default => Shared.Value;

    private static readonly string[] Phonemes =
    {
        "<pad>", "<unk>", "<s>", "</s>",
        "AA0", "AA1", "AA2", "AE0", "AE1", "AE2", "AH0", "AH1", "AH2", "AO0",
        "AO1", "AO2", "AW0", "AW1", "AW2", "AY0", "AY1", "AY2", "B", "CH", "D", "DH",
        "EH0", "EH1", "EH2", "ER0", "ER1", "ER2", "EY0", "EY1",
        "EY2", "F", "G", "HH",
        "IH0", "IH1", "IH2", "IY0", "IY1", "IY2", "JH", "K", "L",
        "M", "N", "NG", "OW0", "OW1",
        "OW2", "OY0", "OY1", "OY2", "P", "R", "S", "SH", "T", "TH",
        "UH0", "UH1", "UH2", "UW",
        "UW0", "UW1", "UW2", "V", "W", "Y", "Z", "ZH"
    };

    private const int UnkGrapheme = 1;
    private const int EndGrapheme = 2;
    private const int StartPhoneme = 2;
    private const int EndPhoneme = 3;

    private readonly Matrix _encEmb, _encWih, _encWhh, _decEmb, _decWih, _decWhh, _fcW;
    private readonly float[] _encBih, _encBhh, _decBih, _decBhh, _fcB;
    private readonly int _hidden;

    private NeuralG2P()
    {
        var tensors = LoadNpz("g2p_en_checkpoint20.npz");
        _encEmb = tensors["enc_emb"];
        _encWih = tensors["enc_w_ih"];
        _encWhh = tensors["enc_w_hh"];
        _encBih = tensors["enc_b_ih"].Data;
        _encBhh = tensors["enc_b_hh"].Data;
        _decEmb = tensors["dec_emb"];
        _decWih = tensors["dec_w_ih"];
        _decWhh = tensors["dec_w_hh"];
        _decBih = tensors["dec_b_ih"].Data;
        _decBhh = tensors["dec_b_hh"].Data;
        _fcW = tensors["fc_w"];
        _fcB = tensors["fc_b"].Data;
        _hidden = _encWhh.Cols;

        if (_decEmb.Rows != Phonemes.Length || _fcW.Rows != Phonemes.Length || _encWih.Rows != 3 * _hidden)
        {
            throw new InvalidDataException("g2p_en checkpoint does not match the expected vocabulary or GRU shape.");
        }
    }

    /// <summary>
    /// Predicts ARPAbet phonemes (with stress digits) for one word.
    /// Non a-z characters are ignored; returns an empty array for words without letters.
    /// </summary>
    public string[] Predict(string word)
    {
        var letters = new List<int>(word.Length + 1);
        foreach (char ch in word.ToLowerInvariant())
        {
            if (ch >= 'a' && ch <= 'z') letters.Add(ch - 'a' + 3);
        }
        if (letters.Count == 0) return Array.Empty<string>();
        letters.Add(EndGrapheme);

        var h = new float[_hidden];
        var scratch = new GruScratch(_hidden);
        foreach (int g in letters)
        {
            GruCell(_encEmb.Row(g), h, _encWih, _encWhh, _encBih, _encBhh, scratch);
        }

        var result = new List<string>();
        int prev = StartPhoneme;
        int maxSteps = Math.Max(20, letters.Count * 2 + 4);
        var logits = new float[Phonemes.Length];
        for (int step = 0; step < maxSteps; step++)
        {
            GruCell(_decEmb.Row(prev), h, _decWih, _decWhh, _decBih, _decBhh, scratch);
            MatVec(_fcW, h, _fcB, logits);
            int best = 0;
            for (int k = 1; k < logits.Length; k++)
            {
                if (logits[k] > logits[best]) best = k;
            }
            if (best == EndPhoneme) break;
            if (best > EndPhoneme) result.Add(Phonemes[best]);
            prev = best;
        }
        return result.ToArray();
    }

    private sealed class GruScratch
    {
        public readonly float[] Ih, Hh;
        public GruScratch(int hidden)
        {
            Ih = new float[3 * hidden];
            Hh = new float[3 * hidden];
        }
    }

    /// <summary>PyTorch GRU cell (gate order r, z, n); updates <paramref name="h"/> in place.</summary>
    private void GruCell(ReadOnlySpan<float> x, float[] h, Matrix wih, Matrix whh, float[] bih, float[] bhh, GruScratch s)
    {
        MatVec(wih, x, bih, s.Ih);
        MatVec(whh, h, bhh, s.Hh);
        int n = _hidden;
        for (int i = 0; i < n; i++)
        {
            float r = Sigmoid(s.Ih[i] + s.Hh[i]);
            float z = Sigmoid(s.Ih[n + i] + s.Hh[n + i]);
            float cand = MathF.Tanh(s.Ih[2 * n + i] + r * s.Hh[2 * n + i]);
            h[i] = (1 - z) * cand + z * h[i];
        }
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    private static void MatVec(Matrix m, ReadOnlySpan<float> x, float[] bias, float[] output)
    {
        for (int r = 0; r < m.Rows; r++)
        {
            output[r] = Dot(m.Row(r), x) + bias[r];
        }
    }

    private static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        int i = 0;
        float sum = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
        {
            var acc = Vector<float>.Zero;
            var va = MemoryMarshal.Cast<float, Vector<float>>(a);
            var vb = MemoryMarshal.Cast<float, Vector<float>>(b);
            for (int k = 0; k < va.Length; k++) acc += va[k] * vb[k];
            sum = Vector.Dot(acc, Vector<float>.One);
            i = va.Length * Vector<float>.Count;
        }
        for (; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private sealed class Matrix
    {
        public readonly float[] Data;
        public readonly int Rows;
        public readonly int Cols;

        public Matrix(float[] data, int rows, int cols)
        {
            Data = data;
            Rows = rows;
            Cols = cols;
        }

        public ReadOnlySpan<float> Row(int r) => new(Data, r * Cols, Cols);
    }

    // ------------------------------------------------------------------
    // .npz loading (zip of .npy arrays, little-endian float32, C order)
    // ------------------------------------------------------------------

    private static readonly Regex ShapeRegex = new(@"'shape':\s*\(([^)]*)\)", RegexOptions.Compiled);

    private static Dictionary<string, Matrix> LoadNpz(string fileName)
    {
        string resource = $"Flama.G2P.English.Data.{fileName}";
        using Stream stream = typeof(NeuralG2P).Assembly.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Embedded G2P resource '{resource}' is missing.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var tensors = new Dictionary<string, Matrix>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.EndsWith(".npy", StringComparison.Ordinal)) continue;
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            tensors[entry.FullName[..^4]] = ParseNpy(ms.ToArray(), entry.FullName);
        }
        return tensors;
    }

    private static Matrix ParseNpy(byte[] bytes, string name)
    {
        if (bytes.Length < 10 || bytes[0] != 0x93 || Encoding.ASCII.GetString(bytes, 1, 5) != "NUMPY")
        {
            throw new InvalidDataException($"{name} is not a .npy array.");
        }
        int major = bytes[6];
        int headerLen = major == 1
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2))
            : (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
        int offset = major == 1 ? 10 : 12;
        string header = Encoding.Latin1.GetString(bytes, offset, headerLen);
        if (!header.Contains("'<f4'") || header.Contains("'fortran_order': True"))
        {
            throw new InvalidDataException($"{name} must be a C-ordered little-endian float32 array.");
        }

        var shapeMatch = ShapeRegex.Match(header);
        var dims = new List<int>();
        foreach (string part in shapeMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            dims.Add(int.Parse(part, CultureInfo.InvariantCulture));
        }
        int rows = dims.Count == 2 ? dims[0] : 1;
        int cols = dims.Count == 2 ? dims[1] : dims[0];

        int dataStart = offset + headerLen;
        var data = new float[rows * cols];
        var span = bytes.AsSpan(dataStart, data.Length * 4);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(i * 4, 4));
        }
        return new Matrix(data, rows, cols);
    }
}
