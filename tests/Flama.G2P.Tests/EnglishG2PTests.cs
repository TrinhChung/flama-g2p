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
        Assert.Equal("ɡˈʊd mˈɔɹnᵻŋ", goodMorning);
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
        Assert.Equal("ˈæpəlz", apples);

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

        // AI -> ˈAˈI
        string ai = g2p.Phonemize("AI");
        Assert.Equal("ˈAˈI", ai);

        // USA -> jˈuˈɛsˈA
        string usa = g2p.Phonemize("USA");
        Assert.Equal("jˈuˈɛsˈA", usa);
    }

    [Fact]
    public void EnglishG2P_Phonemize_OovBehavior_ReturnOriginalByDefault()
    {
        var g2p = new EnglishG2P(); // default OovStrategy is ReturnOriginal
        string ipa = g2p.Phonemize("xyzabc");
        Assert.Equal("xyzabc", ipa);
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
