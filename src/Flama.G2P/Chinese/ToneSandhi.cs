// Copyright (c) 2026 Trinh Chung.
//
// Licensed under the PolyForm Noncommercial License 1.0.0
// for non-commercial use.
//
// Commercial use requires a separate license from Flama.
// See LICENSE and COMMERCIAL_LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Flama.G2P.Chinese;

/// <summary>
/// Mandarin Chinese Tone Sandhi engine ported from Baidu PaddleSpeech and Misaki.
/// Handles neutral tone (轻声), 'yi' and 'bu' sandhi, and advanced 3rd-tone prosodic sandhi.
/// </summary>
public class ToneSandhi
{
    private static readonly HashSet<string> MustNeuralToneWords = new(StringComparer.Ordinal)
    {
        "麻烦", "麻利", "鸳鸯", "高粱", "骨头", "骆驼", "马虎", "首饰", "馒头", "馄饨", "风筝",
        "难为", "队伍", "阔气", "闺女", "门道", "锄头", "铺盖", "铃铛", "铁匠", "钥匙", "里脊",
        "里头", "部分", "那么", "道士", "造化", "迷糊", "连累", "这么", "这个", "运气", "过去",
        "软和", "转悠", "踏实", "跳蚤", "跟头", "趔趄", "财主", "豆腐", "讲究", "记性", "记号",
        "认识", "规矩", "见识", "裁缝", "补丁", "衣裳", "衣服", "衙门", "街坊", "行李", "行当",
        "蛤蟆", "蘑菇", "薄荷", "葫芦", "葡萄", "萝卜", "荸荠", "苗条", "苗头", "苍蝇", "芝麻",
        "舒服", "舒坦", "舌头", "自在", "膏药", "脾气", "脑袋", "脊梁", "能耐", "胳膊", "胭脂",
        "胡萝", "胡琴", "胡同", "聪明", "耽误", "耽搁", "耷拉", "耳朵", "老爷", "老实", "老婆",
        "戏弄", "将军", "翻腾", "罗嗦", "罐头", "编辑", "结实", "红火", "累赘", "糨糊", "糊涂",
        "精神", "粮食", "簸箕", "篱笆", "算计", "算盘", "答应", "笤帚", "笑语", "笑话", "窟窿",
        "窝囊", "窗户", "稳当", "稀罕", "称呼", "秧歌", "秀气", "秀才", "福气", "祖宗", "砚台",
        "码头", "石榴", "石头", "石匠", "知识", "眼睛", "眯缝", "眨巴", "眉毛", "相声", "盘算",
        "白净", "痢疾", "痛快", "疟疾", "疙瘩", "疏忽", "畜生", "生意", "甘蔗", "琵琶", "琢磨",
        "琉璃", "玻璃", "玫瑰", "玄乎", "狐狸", "状元", "特务", "牲口", "牙碜", "牌楼", "爽快",
        "爱人", "热闹", "烧饼", "烟筒", "烂糊", "点心", "炊帚", "灯笼", "火候", "漂亮", "滑溜",
        "溜达", "温和", "清楚", "消息", "浪头", "活泼", "比方", "正经", "欺负", "模糊", "槟榔",
        "棺材", "棒槌", "棉花", "核桃", "栅栏", "柴火", "架势", "枕头", "枇杷", "机灵", "本事",
        "木头", "木匠", "朋友", "月饼", "月亮", "暖和", "明白", "时候", "新鲜", "故事", "收拾",
        "收成", "提防", "挖苦", "挑剔", "指甲", "指头", "拾掇", "拳头", "拨弄", "招牌", "招呼",
        "抬举", "护士", "折腾", "扫帚", "打量", "打算", "打扮", "打听", "打发", "扎实", "扁担",
        "戒指", "懒得", "意识", "意思", "悟性", "怪物", "思量", "怎么", "念头", "念叨", "别人",
        "快活", "忙活", "志气", "心思", "得罪", "张罗", "弟兄", "开通", "应酬", "庄稼", "干事",
        "帮手", "帐篷", "希罕", "师父", "师傅", "巴结", "巴掌", "差事", "工夫", "岁数", "屁股",
        "尾巴", "少爷", "小气", "小伙", "将就", "对头", "对付", "寡妇", "家伙", "客气", "实在",
        "官司", "学问", "字号", "嫁妆", "媳妇", "媒人", "婆家", "娘家", "委屈", "姑娘", "姐夫",
        "妯娌", "妥当", "妖精", "奴才", "女婿", "头发", "太阳", "大爷", "大方", "大意", "大夫",
        "多少", "多么", "外甥", "壮实", "地道", "地方", "在乎", "困难", "嘴巴", "嘱咐", "嘟囔",
        "嘀咕", "喜欢", "喇嘛", "喇叭", "商量", "唾沫", "哑巴", "哈欠", "哆嗦", "咳嗽", "和尚",
        "告诉", "告示", "含糊", "吓唬", "后头", "名字", "名堂", "合同", "吆喝", "叫唤", "口袋",
        "厚道", "厉害", "千斤", "包袱", "包涵", "匀称", "勤快", "动静", "动弹", "功夫", "力气",
        "前头", "刺猬", "刺激", "别扭", "利落", "利索", "利害", "分析", "出息", "凑合", "凉快",
        "冷战", "冤枉", "冒失", "养活", "关系", "先生", "兄弟", "便宜", "使唤", "佩服", "作坊",
        "体面", "位置", "似的", "伙计", "休息", "什么", "人家", "亲戚", "亲家", "交情", "云彩",
        "事情", "买卖", "主意", "丫头", "丧气", "两口", "东西", "东家", "世故", "不由", "下水",
        "下巴", "上头", "上司", "丈夫", "丈人", "一辈", "那个", "菩萨", "父亲", "母亲", "咕噜",
        "邋遢", "费用", "冤家", "甜头", "介绍", "荒唐", "大人", "泥鳅", "幸福", "熟悉", "计划",
        "扑腾", "蜡烛", "姥爷", "照顾", "喉咙", "吉他", "弄堂", "蚂蚱", "凤凰", "拖沓", "寒碜",
        "糟蹋", "倒腾", "报复", "逻辑", "盘缠", "喽啰", "牢骚", "咖喱", "扫把", "惦记", "孩子"
    };

    private static readonly HashSet<string> MustNotNeuralToneWords = new(StringComparer.Ordinal)
    {
        "男子", "女子", "分子", "原子", "量子", "莲子", "石子", "瓜子", "电子", "人人", "虎虎",
        "幺幺", "干嘛", "学子", "哈哈", "数数", "袅袅", "局地", "以下", "娃哈哈", "花花草草", "留得",
        "耕地", "想想", "熙熙", "攘攘", "卵子", "死死", "冉冉", "恳恳", "佼佼", "吵吵", "打打",
        "考考", "整整", "莘莘", "落地", "算子", "家家户户", "青青", "妻子"
    };

    private static readonly string ModalParticles = "吧呢啊呐噻嘛吖嗨呐哦哒滴哩哟喽啰耶喔诶";

    /// <summary>
    /// Applies neutral tone (5) rules to a word and its pinyins.
    /// </summary>
    public void ApplyNeuralTone(string word, List<string> pinyins)
    {
        if (word.Length != pinyins.Count || MustNotNeuralToneWords.Contains(word))
        {
            return;
        }

        // 1. Reduplication words (AA e.g. 看看, 听听, 常常)
        if (word.Length == 2 && word[0] == word[1])
        {
            pinyins[1] = SetTone(pinyins[1], 5);
            return;
        }

        // 2. Modal particles at the end (吧, 呢, 啊...)
        if (word.Length >= 1 && ModalParticles.Contains(word[^1]))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
            return;
        }

        // 3. Structural particles: 的, 地, 得
        if (word.Length >= 1 && (word[^1] == '的' || word[^1] == '地' || word[^1] == '得'))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
            return;
        }

        // 4. Suffixes: 们, 子 (e.g. 我们, 孩子, 桌子)
        if (word.Length > 1 && (word[^1] == '们' || word[^1] == '子'))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
            return;
        }

        // 5. Directional complements & suffixes: 上, 下, 起来, 过来, 回去...
        if (word.Length > 1 && (word[^1] == '上' || word[^1] == '下'))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
            return;
        }

        if (word.Length > 1 && (word[^1] == '来' || word[^1] == '去') && "上下进出回过起开".Contains(word[^2]))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
            return;
        }

        // 6. Conventional neutral tone lexicon match
        if (MustNeuralToneWords.Contains(word) || (word.Length > 2 && MustNeuralToneWords.Contains(word[^2..])))
        {
            pinyins[^1] = SetTone(pinyins[^1], 5);
        }
    }

    /// <summary>
    /// Applies tone sandhi rules for "不" (bu) across the entire segment.
    /// </summary>
    public void ApplyBuSandhi(string segment, List<string> pinyins)
    {
        if (segment.Length != pinyins.Count) return;

        // Potential complement: V + 不 + C (e.g. 看不懂, 得不到, 去不了)
        for (int i = 0; i < segment.Length - 2; i++)
        {
            if (segment[i + 1] == '不' && pinyins[i + 1].StartsWith("bu", StringComparison.OrdinalIgnoreCase))
            {
                pinyins[i + 1] = SetTone(pinyins[i + 1], 5);
            }
        }

        for (int i = 0; i < pinyins.Count - 1; i++)
        {
            if (segment[i] == '不' && pinyins[i].StartsWith("bu", StringComparison.OrdinalIgnoreCase))
            {
                // "不" before 4th tone changes to 2nd tone (bú), but only if it was citation tone 4
                if (GetTone(pinyins[i]) == 4 && GetTone(pinyins[i + 1]) == 4)
                {
                    pinyins[i] = "bu2";
                }
            }
        }
    }

    /// <summary>
    /// Applies tone sandhi rules for "一" (yi) across the entire segment.
    /// </summary>
    public void ApplyYiSandhi(string segment, List<string> pinyins)
    {
        if (segment.Length != pinyins.Count) return;

        // 1. "一" between reduplicated verbs: 看一看, 听一听, 走一走 -> tone 5
        for (int i = 0; i < segment.Length - 2; i++)
        {
            if (segment[i + 1] == '一' && segment[i] == segment[i + 2] && pinyins[i + 1].StartsWith("yi", StringComparison.OrdinalIgnoreCase))
            {
                pinyins[i + 1] = SetTone(pinyins[i + 1], 5);
            }
        }

        for (int i = 0; i < pinyins.Count - 1; i++)
        {
            if (segment[i] == '一' && pinyins[i].StartsWith("yi", StringComparison.OrdinalIgnoreCase))
            {
                // Skip if part of "第一" or "初一"
                if (i > 0 && (segment[i - 1] == '第' || segment[i - 1] == '初')) continue;

                // Skip if digit sequence
                bool isNumSeq = (i > 0 && char.IsDigit(segment[i - 1])) || (i + 1 < segment.Length && char.IsDigit(segment[i + 1]));
                if (isNumSeq) continue;

                int nextTone = GetTone(pinyins[i + 1]);
                if (nextTone == 4 || nextTone == 5)
                {
                    // "一" before 4th tone (or neutral tone derived from 4th) becomes 2nd tone (yí)
                    pinyins[i] = "yi2";
                }
                else if (nextTone == 1 || nextTone == 2 || nextTone == 3)
                {
                    // "一" before 1st, 2nd, 3rd tone becomes 4th tone (yì)
                    pinyins[i] = "yi4";
                }
            }
        }
    }

    /// <summary>
    /// Applies 3rd-tone sandhi across syllables.
    /// Handles 2-syllable (3+3 -> 2+3), 3-syllable ([1+2] -> 3+2+3 vs [2+1] -> 2+2+3),
    /// and 4-syllable idioms.
    /// </summary>
    public void ApplyThirdToneSandhi(List<string> pinyins)
    {
        if (pinyins.Count < 2) return;

        // 2 syllables: 3 + 3 -> 2 + 3
        if (pinyins.Count == 2)
        {
            if (GetTone(pinyins[0]) == 3 && GetTone(pinyins[1]) == 3)
            {
                pinyins[0] = SetTone(pinyins[0], 2);
            }
            return;
        }

        // 3 syllables: 3 + 3 + 3
        if (pinyins.Count == 3 && GetTone(pinyins[0]) == 3 && GetTone(pinyins[1]) == 3 && GetTone(pinyins[2]) == 3)
        {
            // Default prosodic cadence for disyllabic + monosyllabic [2+1] (e.g. 展览馆) -> 2+2+3
            pinyins[0] = SetTone(pinyins[0], 2);
            pinyins[1] = SetTone(pinyins[1], 2);
            return;
        }

        // General sequential adjacent pair sandhi
        for (int i = 0; i < pinyins.Count - 1; i++)
        {
            if (GetTone(pinyins[i]) == 3 && GetTone(pinyins[i + 1]) == 3)
            {
                pinyins[i] = SetTone(pinyins[i], 2);
            }
        }
    }

    private static int GetTone(string pinyin)
    {
        if (pinyin.Length > 0 && char.IsDigit(pinyin[^1]))
        {
            return pinyin[^1] - '0';
        }
        return 5;
    }

    private static string SetTone(string pinyin, int tone)
    {
        if (pinyin.Length > 0 && char.IsDigit(pinyin[^1]))
        {
            return pinyin[..^1] + tone;
        }
        return pinyin + tone;
    }
}
