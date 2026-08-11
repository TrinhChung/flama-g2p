// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using Flama.G2P.Chinese;
using Xunit;

namespace Flama.G2P.Tests;

public class NumberNormalizationTests
{
    [Theory]
    [InlineData(0, "零")]
    [InlineData(1, "一")]
    [InlineData(10, "十")]
    [InlineData(11, "十一")]
    [InlineData(20, "二十")]
    [InlineData(21, "二十一")]
    [InlineData(100, "一百")]
    [InlineData(101, "一百零一")]
    [InlineData(110, "一百一十")]
    [InlineData(105, "一百零五")]
    [InlineData(1000, "一千")]
    [InlineData(1001, "一千零一")]
    [InlineData(1010, "一千零一十")]
    [InlineData(1100, "一千一百")]
    [InlineData(10000, "一万")]
    [InlineData(10001, "一万零一")]
    [InlineData(10010, "一万零一十")]
    [InlineData(10100, "一万零一百")]
    [InlineData(11000, "一万一千")]
    public void ChineseG2P_IntegerToChinese_ConvertsNumbersCorrectly(long number, string expected)
    {
        string actual = ChineseG2P.IntegerToChinese(number);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ChineseG2P_NormalizeNumbers_ReplacesDigitsInString()
    {
        string input = "我有105个苹果和10000本书。";
        string expected = "我有一百零五个苹果和一万本书。"; // Punctuation will map space later, but number replacement is raw
        
        string actual = ChineseG2P.NormalizeNumbers(input);
        Assert.Equal(expected, actual);
    }
}
