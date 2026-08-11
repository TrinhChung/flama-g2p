# Third-Party Notices - Flama G2P

This file contains licensing and source notices for third-party data and dictionaries used by Flama G2P.

---

## 1. CMUdict (Carnegie Mellon Pronouncing Dictionary)
- **Source:** cmusphinx/cmudict (https://github.com/cmusphinx/cmudict)
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
