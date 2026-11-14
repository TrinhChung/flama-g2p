# Flama G2P

Flama G2P is a lightweight, standalone Grapheme-to-Phoneme (G2P) conversion library for .NET 8, designed to prepare phoneme inputs for neural Text-to-Speech (TTS) pipelines like Kokoro.

It is **Source Available** under a dual-licensing model: free for non-commercial use, and requiring a separate commercial license for commercial applications.

---

## Features
- **English**: Produces the same phoneme alphabet Kokoro was trained on, following [misaki](https://github.com/hexgrad/misaki):
  - misaki's gold/silver lexicons (~180k entries each for American and British English), embedded in the assembly.
  - misaki's rules for function words (*a, an, the, to, in, I*), capitalization stress, spelled acronyms, compound stress and *-s / -ed / -ing* stemming.
  - Heteronyms chosen from local context (*to record* vs *the record*, *he read* vs *will read*, *used to*).
  - Out-of-vocabulary words predicted by the [g2p_en](https://github.com/Kyubyong/g2p) GRU model, ported to C# (no Python or native dependency).
  - Optional CMUdict file as an extra lexicon, mainly for proper names.
  - Text normalization for numbers, years, decades, currencies, times, ISO dates, units, ordinals and common abbreviations.
- **Mandarin Chinese**: Normalizes Chinese punctuation and numbers, segments words using an extensible phrase override lexicon (to handle polyphonic characters), applies tone sandhi rules, and converts Pinyin to IPA with explicit tone arrows.
- **Mixed-Language Support**: Seamlessly processes intermixed Chinese and English text.

---

## Installation & Build

Build the project from the root folder:
```bash
dotnet build Flama.G2P.sln -c Release
```

Run tests:
```bash
dotnet test Flama.G2P.sln -c Release
```

Pack NuGet package:
```bash
dotnet pack src/Flama.G2P/Flama.G2P.csproj -c Release
```

---

## Usage

### English Example
```csharp
using Flama.G2P.English;

var english = new EnglishG2P();                     // American English
string ipa = english.Phonemize("The water is better.");
Console.WriteLine(ipa); // "ðə wˈɔTəɹ ɪz bˈɛTəɹ."

var british = new EnglishG2P(british: true);       // Kokoro "b" voices
Console.WriteLine(british.Phonemize("The water is better.")); // "ðə wˈɔːtə ɪz bˈɛtə."

// Optional CMUdict for names, and a different strategy for unknown words
var withCmu = new EnglishG2P(new CmuDictionary("cmudict.dict"), OovStrategy.Neural);
```

### Chinese Example
```csharp
using Flama.G2P.Chinese;

var chinese = new ChineseG2P();
string ipa = chinese.Phonemize("你好");
Console.WriteLine(ipa); // "ni↗ xau̯↓"
```

### Mixed-Language Support
By default, `ChineseG2P` handles mixed-language text by routing English segments to a default internal `EnglishG2P` instance. You can also inject a custom English fallback:
```csharp
var customEnglish = new EnglishG2P(oovStrategy: OovStrategy.ReturnOriginal);
var chinese = new ChineseG2P(englishG2p: customEnglish);
string ipa = chinese.Phonemize("你好 test");
```

---

## Known Limitations

- **English output format**: Output targets Kokoro v1.0 (misaki before 2.0): the American flap is written `T` and the glottal stop `t`. Set `KokoroV1Symbols = false` for raw misaki 2.0 symbols (`ɾ`, `ʔ`).
- **English part of speech**: misaki uses spaCy to tag words; Flama G2P uses short local rules instead. Heteronyms without a clear cue (*wind the clock* at the start of a sentence, *I read* in present tense) fall back to the dictionary default.
- **English OOV (Out-of-Vocabulary) words**: The default `OovStrategy.Neural` predicts pronunciations with the g2p_en model. On rare words absent from CMUdict it matches misaki's gold pronunciation exactly about 25% of the time, with a phoneme error rate around 23% (the letter rules in `OovStrategy.SimpleRules`: about 44%). British pronunciations for OOV words are converted from American predictions.
- **Size and start-up**: The embedded English data adds about 6 MB to the assembly. Each dialect's lexicon loads once per process (a few hundred milliseconds) and is shared by all `EnglishG2P` instances.
- **Chinese Polyphones**: Mandarin has many multi-pronunciation characters (多音字). The `PhrasePinyinDictionary` supports phrase-based overrides, but its default built-in lexicon is limited. It is designed to be easily extensible.
- **Tone Sandhi**: Third-tone and Yi/Bu sandhi are applied using local adjacent-pair rules. It does not perform full natural language processing (NLP) prosodic phrase segmentation.

---

## License

Flama G2P is source-available.

It is available for personal, educational, research, and other non-commercial use under the PolyForm Noncommercial License 1.0.0.

Commercial use requires a separate license from Flama.

For full licensing details, see:
- [LICENSE](./LICENSE) — Non-commercial license terms.
- [COMMERCIAL_LICENSE.md](./COMMERCIAL_LICENSE.md) — Commercial license information and contact instructions.
- [CONTRIBUTING.md](./CONTRIBUTING.md) — Rules for contributing to this project.

---

## Third-Party Resources

Flama G2P makes use of third-party datasets and resources:
- **misaki** lexicons and ported English G2P logic: Apache License 2.0.
- **g2p_en** neural model weights and ported inference: Apache License 2.0.
- **CMUdict** (optional, not bundled): Permissive BSD-style license.
- **pinyin_dict.txt**: Derived from the Python `pypinyin` package during integration (provenance is undergoing verification, marked as a release blocker).

These resources are subject to their own respective upstream licenses and are not governed by the Flama G2P license. See [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) for details.
