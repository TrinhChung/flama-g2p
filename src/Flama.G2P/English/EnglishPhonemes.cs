// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.
//
// Phoneme inventory and stress handling follow misaki (hexgrad/misaki, Apache-2.0),
// the G2P engine Kokoro-82M was trained with. See THIRD_PARTY_NOTICES.md.

using System;
using System.Linq;

namespace Flama.G2P.English;

/// <summary>
/// Misaki English phoneme constants and stress helpers.
/// </summary>
public static class EnglishPhonemes
{
    public const char PrimaryStress = 'ˈ';
    public const char SecondaryStress = 'ˌ';

    /// <summary>Vowel symbols (US and GB) that can carry stress.</summary>
    public const string Vowels = "AIOQWYaiuæɑɒɔəɛɜɪʊʌᵻ";

    public const string Consonants = "bdfhjklmnpstvwzðŋɡɹɾʃʒʤʧθ";

    /// <summary>Sounds after which an American /t/ becomes a flap before an unstressed vowel.</summary>
    public const string UsTaus = "AIOWYiuæɑəɛɪɹʊʌ";

    public const string Diphthongs = "AIOQWYʤʧ";

    /// <summary>Punctuation Kokoro keeps in its phoneme stream.</summary>
    public const string Puncts = ";:,.!?—…\"“”";

    public const string NonQuotePuncts = ";:,.!?—…";

    /// <summary>Characters inside a word that carry no sound of their own.</summary>
    public const string SubtokenJunks = "',-._‘’/";

    public static bool IsVowel(char c) => Vowels.IndexOf(c) >= 0;

    public static bool IsStress(char c) => c == PrimaryStress || c == SecondaryStress;

    public static int StressWeight(string? ps)
    {
        if (string.IsNullOrEmpty(ps)) return 0;
        int weight = 0;
        foreach (char c in ps) weight += Diphthongs.IndexOf(c) >= 0 ? 2 : 1;
        return weight;
    }

    /// <summary>
    /// Port of misaki <c>apply_stress</c>. <paramref name="stress"/> follows misaki's scale:
    /// null keeps the input, below -1 strips all stress, -1 (or 0 / -0.5 with a primary present)
    /// demotes, 0..1 adds secondary stress to unstressed words, and above 1 adds primary stress.
    /// </summary>
    public static string? ApplyStress(string? ps, double? stress)
    {
        if (ps == null || stress == null) return ps;
        double s = stress.Value;
        bool hasPrimary = ps.IndexOf(PrimaryStress) >= 0;
        bool hasSecondary = ps.IndexOf(SecondaryStress) >= 0;
        bool hasAnyStress = hasPrimary || hasSecondary;

        if (s < -1)
        {
            return ps.Replace(PrimaryStress.ToString(), "").Replace(SecondaryStress.ToString(), "");
        }
        if (s == -1 || ((s == 0 || s == -0.5) && hasPrimary))
        {
            return ps.Replace(SecondaryStress.ToString(), "").Replace(PrimaryStress, SecondaryStress);
        }
        if ((s == 0 || s == 0.5 || s == 1) && !hasAnyStress)
        {
            return ps.Any(IsVowel) ? Restress(SecondaryStress, ps) : ps;
        }
        if (s >= 1 && !hasPrimary && hasSecondary)
        {
            return ps.Replace(SecondaryStress, PrimaryStress);
        }
        if (s > 1 && !hasAnyStress)
        {
            return ps.Any(IsVowel) ? Restress(PrimaryStress, ps) : ps;
        }
        return ps;
    }

    /// <summary>Places <paramref name="mark"/> right before the first vowel of an unstressed string.</summary>
    private static string Restress(char mark, string ps)
    {
        for (int i = 0; i < ps.Length; i++)
        {
            if (IsVowel(ps[i])) return ps.Insert(i, mark.ToString());
        }
        return ps;
    }

    /// <summary>
    /// Kokoro-82M v1.0 was trained on misaki output with the flap written as <c>T</c>
    /// and the glottal stop as <c>t</c> (misaki <c>version != "2.0"</c>).
    /// </summary>
    public static string ToKokoroV1(string ps) => ps.Replace('ɾ', 'T').Replace('ʔ', 't');
}
