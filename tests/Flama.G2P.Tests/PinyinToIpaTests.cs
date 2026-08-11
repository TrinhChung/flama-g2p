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

public class PinyinToIpaTests
{
    [Theory]
    [InlineData("dui1", "duei1")]
    [InlineData("liu3", "liou3")]
    [InlineData("lun2", "luen2")]
    [InlineData("jun4", "jun4")] // Starts with j -> stays jun4, but will normalize Jqxu to jün4
    [InlineData("yun2", "yun2")] // Starts with y -> stays yun2
    public void PinyinToIpa_NormalizePinyinOrthography_ConvertsCorrectly(string pinyin, string expected)
    {
        string actual = PinyinToIpa.NormalizePinyinOrthography(pinyin);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ju1", "jü1")]
    [InlineData("quan2", "qüan2")]
    [InlineData("xun4", "xün4")]
    [InlineData("jue2", "jüe2")]
    [InlineData("gu1", "gu1")] // Starts with g -> stays gu1
    public void PinyinToIpa_NormalizeJqxu_ConvertsCorrectly(string pinyin, string expected)
    {
        string actual = PinyinToIpa.NormalizeJqxu(pinyin);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("zhong1", "ʈʂɔ˥ŋ")]
    [InlineData("ju1", "ʨy˥")] // jü1 -> ʨ + ü + 1 -> ʨ + y˥
    [InlineData("qu2", "ʨʰy˧˥")] // qü2 -> ʨʰ + y˧˥
    [InlineData("xu3", "ɕy˧˩˧")] // xü3 -> ɕ + y˧˩˧
    [InlineData("jue2", "ʨɥe˧˥")] // jüe2 -> ʨ + ɥe˧˥ -> ʨ + ɥe˧˥
    [InlineData("quan2", "ʨʰɥɛ˧˥n")] // qüan2 -> ʨʰ + ɥɛn˧˥ -> ʨʰ + ɥɛ˧˥n
    [InlineData("xun4", "ɕy˥˩n")] // xün4 -> ɕ + yn4 -> ɕ + y˥˩n
    public void PinyinToIpa_Convert_MandarinSyllables_ProducesCorrectIPA(string pinyin, string expected)
    {
        string actual = PinyinToIpa.Convert(pinyin);
        Assert.Equal(expected, actual);
    }
}
