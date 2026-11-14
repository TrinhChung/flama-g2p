// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using Flama.G2P.English;
using Xunit;

namespace Flama.G2P.Tests;

public class EnglishG2PTests
{
    [Fact]
    public void EnglishG2P_Phonemize_StandardWords_ReturnsCorrectKokoroIPA()
    {
        var g2p = new EnglishG2P();
        
        string hello = g2p.Phonemize("hello");
        string world = g2p.Phonemize("world");
        string goodMorning = g2p.Phonemize("good morning");

        Assert.Equal("həlˈO", hello);
        Assert.Equal("wˈɜɹld", world);
        Assert.Equal("ɡˈʊd mˈɔɹnɪŋ", goodMorning);
    }

    [Fact]
    public void EnglishG2P_Phonemize_StressConversion_PreservesStresses()
    {
        var g2p = new EnglishG2P();
        string ipa = g2p.Phonemize("hello");
        
        // CMUdict for hello is HH AH0 L OW1
        // AH0 -> ə, OW1 -> ˈO
        // Total is həlˈO
        Assert.Equal("həlˈO", ipa);
    }

    [Fact]
    public void EnglishG2P_Phonemize_PunctuationAndEmpty_PreservesWhitespaceAndPunctuation()
    {
        var g2p = new EnglishG2P();
        
        Assert.Equal("", g2p.Phonemize(""));
        Assert.Equal("", g2p.Phonemize("   "));
        Assert.Equal("həlˈO, wˈɜɹld!", g2p.Phonemize("hello, world!"));
    }

    [Fact]
    public void EnglishG2P_Phonemize_NumberNormalization_ConvertsNumbersToWords()
    {
        var g2p = new EnglishG2P();

        // 100 -> one hundred -> wˈʌn hˈʌndɹəd
        string hundred = g2p.Phonemize("100");
        Assert.Contains("wˈʌn", hundred);
        Assert.Contains("hˈʌndɹəd", hundred);

        // 1st -> first -> fˈɜɹst
        string first = g2p.Phonemize("1st");
        Assert.Equal("fˈɜɹst", first);

        // 50% -> fifty percent
        string percent = g2p.Phonemize("50%");
        Assert.Contains("fˈɪfti", percent);
        Assert.Contains("pəɹsˈɛnt", percent);

        // $20 -> twenty dollars
        string dollars = g2p.Phonemize("$20");
        Assert.Contains("twˈɛnti", dollars);
        Assert.Contains("dˈɑləɹz", dollars);
    }

    [Fact]
    public void EnglishG2P_Phonemize_MorphologicalStemming_HandlesPluralsAndPastTense()
    {
        var g2p = new EnglishG2P();

        // Plural: books (stem: book /bˈʊk/ + s) -> bˈʊks
        string books = g2p.Phonemize("books");
        Assert.Equal("bˈʊks", books);

        // Plural: apples (stem: apple /æpəl/ + z) -> ˈæpəlz
        string apples = g2p.Phonemize("apples");
        Assert.Equal("ˈæpᵊlz", apples);

        // Plural: cars (stem: car /kˈɑɹ/ + z) -> kˈɑɹz
        string cars = g2p.Phonemize("cars");
        Assert.Equal("kˈɑɹz", cars);
    }

    [Fact]
    public void EnglishG2P_Phonemize_ContextualLookahead_SelectsCorrectArticleForm()
    {
        var g2p = new EnglishG2P();

        // "the" before vowel -> ði
        string theApple = g2p.Phonemize("the apple");
        Assert.StartsWith("ði", theApple);

        // "the" before consonant -> ðə
        string theBook = g2p.Phonemize("the book");
        Assert.StartsWith("ðə", theBook);
    }

    [Fact]
    public void EnglishG2P_Phonemize_Acronym_SpellsOutLettersWithStress()
    {
        var g2p = new EnglishG2P();

        // misaki gold entry for AI
        Assert.Equal("ˈAˌI", g2p.Phonemize("AI"));

        // Spelled acronyms keep primary stress on the last letter only
        Assert.Equal("jˌuˌɛsˈA", g2p.Phonemize("USA"));
    }

    [Fact]
    public void EnglishG2P_Phonemize_OovBehavior_NeuralByDefault()
    {
        var g2p = new EnglishG2P(); // default OovStrategy is Neural (g2p_en GRU)
        string ipa = g2p.Phonemize("xyzabc");
        Assert.NotEqual("xyzabc", ipa);
        Assert.DoesNotContain("x", ipa);
    }

    [Fact]
    public void EnglishG2P_Phonemize_OovBehavior_ReturnOriginalWhenRequested()
    {
        var g2p = new EnglishG2P(oovStrategy: OovStrategy.ReturnOriginal);
        Assert.Equal("xyzabc", g2p.Phonemize("xyzabc"));
    }

    [Fact]
    public void NeuralG2P_Predict_MatchesUpstreamG2pEnExample()
    {
        // g2p_en README: "activationist" -> AE2 K T IH0 V EY1 SH AH0 N IH0 S T
        Assert.Equal("AE2 K T IH0 V EY1 SH AH0 N IH0 S T", string.Join(' ', NeuralG2P.Default.Predict("activationist")));
    }

    [Fact]
    public void EnglishG2P_Phonemize_MisakiFeatures_FlapReducedFunctionWordsHeteronyms()
    {
        var g2p = new EnglishG2P();
        Assert.Equal("wˈɔTəɹ", g2p.Phonemize("water"));            // US flap, Kokoro v1 'T'
        Assert.Equal("ʌv", g2p.Phonemize("of"));                   // unstressed function word
        Assert.Contains("tə ɹəkˈɔɹd", g2p.Phonemize("I need to record it"));
        Assert.Contains("ðə ɹˈɛkəɹd", g2p.Phonemize("the record"));
        Assert.Contains("jˈust", g2p.Phonemize("I used to live here"));
        Assert.EndsWith("kˈæn.", g2p.Phonemize("Yes, I can."));
    }

    [Fact]
    public void EnglishG2P_Phonemize_British_UsesNonRhoticLexicon()
    {
        var g2p = new EnglishG2P(british: true);
        Assert.Equal("wˈɔːtə", g2p.Phonemize("water"));
    }

    [Theory]
    [InlineData("2000", "two thousand")]
    [InlineData("in 1998.", "in nineteen ninety-eight.")]
    [InlineData("$1.2 million", "one point two million dollars")]
    [InlineData("3D", "three D")]
    [InlineData("2024-01-15", "January fifteenth, twenty twenty-four")]
    public void EnglishTextNormalizer_Normalize_FixedCases(string input, string expected)
    {
        Assert.Equal(expected, EnglishTextNormalizer.Normalize(input));
    }

    [Fact]
    public void EnglishG2P_Phonemize_OovBehavior_SimpleRulesConvertsLetters()
    {
        var g2p = new EnglishG2P(oovStrategy: OovStrategy.SimpleRules);
        string ipa = g2p.Phonemize("shch"); // sh -> ʃ, ch -> ʧ
        Assert.Equal("ʃʧ", ipa);
    }

    [Fact]
    public void EnglishG2P_Phonemize_OovBehavior_ThrowThrowsException()
    {
        var g2p = new EnglishG2P(oovStrategy: OovStrategy.Throw);
        Assert.Throws<KeyNotFoundException>(() => g2p.Phonemize("xyzabc"));
    }
}
