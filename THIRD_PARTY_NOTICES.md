# Third-Party Notices - Flama G2P

This file contains licensing and source notices for third-party data and dictionaries used by Flama G2P.

---

## 1. CMUdict (Carnegie Mellon Pronouncing Dictionary)
- **Source:** cmusphinx/cmudict (https://github.com/cmusphinx/cmudict)
- **Use:** Not bundled. `CmuDictionary` can load a user-supplied `cmudict.dict` file (Flama Audio downloads it when the Kokoro engine is installed); the g2p_en model below was trained on CMUdict.
- **License:** Permissive BSD-style License
- **Notice:**
  Copyright (C) 1993-2015 Carnegie Mellon University. All rights reserved.

  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

  1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
  2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

  THIS SOFTWARE IS PROVIDED BY CARNEGIE MELLON UNIVERSITY ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL CARNEGIE MELLON UNIVERSITY NOR ITS EMPLOYEES BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

---

## 2. pinyin_dict.txt
- **Path:** `src/Flama.G2P/Chinese/pinyin_dict.txt`
- **Source:** Generated during G2P development using the Python `pypinyin` package (https://github.com/mozillazg/python-pinyin).
- **Status:** **`License/source requires verification before public distribution.`** (RELEASE BLOCKER)
- **Recommendation:** 
  Before public production distribution, verify if the internal data derived from `pypinyin` allows commercial redistribution under its MIT license. If there is any ambiguity, replace or regenerate this dictionary from a fully open-source dataset with verified provenance (such as the Unicode Consortium Unihan database or CC-CEDICT).

---

## 3. misaki (English G2P for Kokoro)
- **Source:** hexgrad/misaki (https://github.com/hexgrad/misaki), commit `fba1236595f2d2bf21d414ba6e57d25256afada3`
- **License:** Apache License 2.0 — full text in [licenses/Apache-2.0.txt](./licenses/Apache-2.0.txt). The upstream repository ships no NOTICE file.
- **Copyright:** hexgrad and misaki contributors.
- **Bundled data** (`src/Flama.G2P/English/Data/`, embedded into the assembly). Each file is the upstream JSON from `misaki/data/`, unmodified except for gzip compression:

  | Bundled file | Upstream file | SHA-256 of the decompressed JSON |
  |---|---|---|
  | `misaki_us_gold.json.gz` | `us_gold.json` | `dc414872a49a28ae6c141463d502fd945f3b2fde040484fdc47d00cc4612686f` |
  | `misaki_us_silver.json.gz` | `us_silver.json` | `de8f67be911bb6c659187b4a65fd966b6a30e56350e0f790d763210b053ac475` |
  | `misaki_gb_gold.json.gz` | `gb_gold.json` | `29e62f4b60261c88f7f3c2c7811ca3825978948090b72d2b27d565b729282f71` |
  | `misaki_gb_silver.json.gz` | `gb_silver.json` | `48131e2d92ccc41655f4543e87e0f938e71463eb5a54be7f0693bb712ebb6bce` |

- **Ported code:** `English/EnglishLexicon.cs`, `English/EnglishPhonemes.cs`, `English/EnglishG2P.cs` and the sub-token regular expression in `English/EnglishTokenizer.cs` are C# ports of `misaki/en.py`. Changes from upstream: spaCy tokenization and tagging are replaced by rule-based heuristics, number reading is done by `EnglishTextNormalizer`, the espeak/BART fallback is replaced by the g2p_en model below, and a doubled-consonant `-ed` stem rule was added. Each ported file says so in its header.
- The bundled data and the ported portions remain available under the Apache License 2.0; the PolyForm Noncommercial terms of Flama G2P do not restrict them.

---

## 4. g2p_en (neural out-of-vocabulary model)
- **Source:** Kyubyong/g2p (https://github.com/Kyubyong/g2p), commit `c6439c274c42b9724a7fee1dc07ca6a4c68a0538`
- **License:** Apache License 2.0 — full text in [licenses/Apache-2.0.txt](./licenses/Apache-2.0.txt). The upstream repository ships no NOTICE file.
- **Copyright:** Kyubyong Park and Jongseok Kim.
- **Bundled data:** `src/Flama.G2P/English/Data/g2p_en_checkpoint20.npz` is upstream `g2p_en/checkpoint20.npz`, unmodified (SHA-256 `b8af35e4596d8dd5836dfd3fe9b2ba4f97b9c311efe8879544cbcfcbd566d8c6`).
- **Ported code:** `English/NeuralG2P.cs` re-implements the GRU encoder-decoder inference of `g2p_en/g2p.py` in C#. Change from upstream: the decoder may emit more than 20 phonemes for long words.
- The bundled weights and the ported portion remain available under the Apache License 2.0.
