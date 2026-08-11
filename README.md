# Flama G2P

Flama G2P is a lightweight, standalone Grapheme-to-Phoneme (G2P) conversion library for .NET 8, designed to prepare phoneme inputs for neural Text-to-Speech (TTS) pipelines like Kokoro.

It is **Source Available** under a dual-licensing model: free for non-commercial use, and requiring a separate commercial license for commercial applications.

---

## Features
- **English**: Converts English text to IPA phonemes using a built-in CMU dictionary lookup.
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

var english = new EnglishG2P();
string ipa = english.Phonemize("hello world");
Console.WriteLine(ipa); // "həˈloʊ wˈɝːld"
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
var customEnglish = new EnglishG2P(oovStrategy: OovStrategy.SimpleRules);
var chinese = new ChineseG2P(englishG2p: customEnglish);
string ipa = chinese.Phonemize("你好 test");
```

---

## Known Limitations

- **English Dialect**: The English implementation is primarily based on the CMU Pronouncing Dictionary, which reflects US English pronunciation. It does not provide full native British (UK) dialect rules.
- **English OOV (Out-of-Vocabulary) Fallback**: By default, words not found in the dictionary are returned verbatim (`OovStrategy.ReturnOriginal`). The alternative `OovStrategy.SimpleRules` letter-by-letter mapping is a basic heuristic and is not linguistically accurate for all words.
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
- **CMUdict**: Permissive BSD-style license.
- **pinyin_dict.txt**: Derived from the Python `pypinyin` package during integration (provenance is undergoing verification, marked as a release blocker).

These resources are subject to their own respective upstream licenses and are not governed by the Flama G2P license. See [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) for details.
