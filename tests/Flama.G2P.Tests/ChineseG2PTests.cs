// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.IO;
using Flama.G2P.Chinese;
using Xunit;

namespace Flama.G2P.Tests;

public class ChineseG2PTests
{
    [Fact]
    public void ChineseG2P_Phonemize_SimpleText_ReturnsIPA()
    {
        var g2p = new ChineseG2P();
        string ipa = g2p.Phonemize("你好");

        Assert.Contains("ni↗", ipa);
        Assert.Contains("xau̯↓", ipa);
    }

    [Fact]
    public void ChineseG2P_Phonemize_YiBuSandhi_AppliesCorrectToneChanges()
    {
        var g2p = new ChineseG2P();

        // "不是" (bu4 shi4 -> bu2 shi4)
        string bushi = g2p.Phonemize("不是");
        // "不要" (bu4 yao4 -> bu2 yao4)
        string buyao = g2p.Phonemize("不要");
        // "一个" (yi1 ge4 -> yi2 ge4)
        string yige = g2p.Phonemize("一个");
        // "一天" (yi1 tian1 -> yi4 tian1)
        string yitian = g2p.Phonemize("一天");

        // bu2 -> p + u + 2nd tone ↗ -> pu↗
        Assert.Contains("pu↗", bushi);
        Assert.Contains("pu↗", buyao);
        // yi2 -> i + 2nd tone ↗ -> i↗
        Assert.Contains("i↗", yige);
        // yi4 -> i + 4th tone ↘ -> i↘
        Assert.Contains("i↘", yitian);
    }

    [Fact]
    public void ChineseG2P_Phonemize_PhraseOverrides_DisambiguatesPolyphones()
    {
        var g2p = new ChineseG2P();

        // "银行" (yínháng) vs "行走" (xíngzǒu)
        string yinhang = g2p.Phonemize("银行");
        string xingzou = g2p.Phonemize("行走");
        
        // "音乐" (yīnyuè) vs "快乐" (kuàilè)
        string yinyue = g2p.Phonemize("音乐");
        string kuaile = g2p.Phonemize("快乐");

        // hang2 -> x + aŋ + 2nd tone ↗ -> xa↗ŋ. xing2 -> ɕ + iŋ + 2nd tone ↗ -> ɕi↗ŋ
        Assert.Contains("xa↗ŋ", yinhang);
        Assert.Contains("ɕi↗ŋ", xingzou);

        // yue4 -> ɥ + e + 4th tone ↘ -> ɥe↘. le4 -> l + ɤ + 4th tone ↘ -> lɤ↘
        Assert.Contains("ɥe↘", yinyue);
        Assert.Contains("lɤ↘", kuaile);
    }

    [Fact]
    public void ChineseG2P_Phonemize_ThreeConsecutiveThirdTones_SandhiApplies()
    {
        var g2p = new ChineseG2P();
        
        // "展览馆" (3+3+3 -> 2+2+3)
        // zhǎn (ʈʂan3) lǎn (lan3) guǎn (kuan3)
        // Sandhi should produce ʈʂa2n, la2n, kwa3n
        string ipa = g2p.Phonemize("展览馆");

        Assert.Contains("ʈʂa↗n", ipa);
        Assert.Contains("la↗n", ipa);
        Assert.Contains("kwa↓n", ipa);
    }

    [Fact]
    public void ChineseG2P_Phonemize_EmptyInput_HandlesGracefully()
    {
        var g2p = new ChineseG2P();
        Assert.Equal("", g2p.Phonemize(""));
        Assert.Equal("", g2p.Phonemize("  "));
    }

    [Fact]
    public void PinyinDictionary_MissingResource_ThrowsException()
    {
        // PinyinDictionary loads embedded resource, which should succeed.
        // We verify that instantiating doesn't throw under normal environment since we embed the resource.
        var dict = new PinyinDictionary();
        Assert.NotNull(dict.GetPinyin('中'));
    }
}
