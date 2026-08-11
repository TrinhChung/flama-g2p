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
    public void EnglishG2P_Phonemize_StandardWords_ReturnsCorrectIPA()
    {
        var g2p = new EnglishG2P();
        
        string hello = g2p.Phonemize("hello");
        string world = g2p.Phonemize("world");
        string goodMorning = g2p.Phonemize("good morning");

        Assert.Contains("həlˈoʊ", hello);
        Assert.Contains("wˈɝld", world);
        Assert.Contains("ɡˈʊd", goodMorning);
        Assert.Contains("mˈɔɹnᵻŋ", goodMorning);
    }

    [Fact]
    public void EnglishG2P_Phonemize_StressConversion_PreservesStresses()
    {
        var g2p = new EnglishG2P();
        string ipa = g2p.Phonemize("hello");
        
        // CMUdict for hello is HH AH0 L OW1
        // AH0 -> ə, OW1 -> ˈoʊ
        // Total is həlˈoʊ
        Assert.Equal("həlˈoʊ", ipa);
    }

    [Fact]
    public void EnglishG2P_Phonemize_PunctuationAndEmpty_PreservesWhitespaceAndPunctuation()
    {
        var g2p = new EnglishG2P();
        
        Assert.Equal("", g2p.Phonemize(""));
        Assert.Equal("", g2p.Phonemize("   "));
        Assert.Equal("həlˈoʊ, wˈɝld!", g2p.Phonemize("hello, world!"));
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
