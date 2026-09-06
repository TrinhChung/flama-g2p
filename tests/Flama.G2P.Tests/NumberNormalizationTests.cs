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

    [Theory]
    [InlineData("圆周率是3.14159", "圆周率是三点一四一五九")]
    [InlineData("胜率达到85%", "胜率达到百分之八十五")]
    [InlineData("完成1/3的任务", "完成三分之一的任务")]
    [InlineData("今天是2026年9月6日", "今天是二零二六年九月六日")]
    [InlineData("现在是15:30", "现在是十五点半")]
    [InlineData("发车时间是08:05", "发车时间是八点五分")]
    [InlineData("我有2个苹果和2本书", "我有两个苹果和两本书")]
    [InlineData("温度是37℃", "温度是三十七摄氏度")]
    [InlineData("价格是$100", "价格是一百美元")]
    public void ChineseTextNormalizer_Normalize_HandlesNonStandardWords(string input, string expected)
    {
        string actual = ChineseTextNormalizer.Normalize(input);
        Assert.Equal(expected, actual);
    }
}
