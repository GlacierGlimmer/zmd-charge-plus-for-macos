# Offline fallback fonts

ECP bundles these SIL Open Font License 1.1 fonts so supported language names and HUD text do not depend solely on installed system fonts.

| Asset | Script | Official source |
| --- | --- | --- |
| NotoSerifTibetan.ttf | Tibetan | [Noto Serif Tibetan](https://github.com/notofonts/tibetan) |
| NotoSansGothic-Regular.ttf | Gothic | [Noto Gothic font builds](https://notofonts.github.io/gothic/) |
| NotoSansDeseret-Regular.ttf | Deseret, including a letter used by upside-down English | [Noto Deseret font builds](https://notofonts.github.io/deseret/) |

The adjacent `OFL-*.txt` files contain the corresponding license and copyright notices. All three license notices accompany desktop publish outputs, Windows Store packages and Android font assets.

The two supplemental font downloads are the official `googlefonts/ttf/*-Regular.ttf` builds. Their SHA-256 digests are:

- Gothic: `535169be262d111f61e5a20455ec2a62dd5fc033a002e0caf8f41633f1877fb0`
- Deseret: `e5ab632f0c9ef73605bbfada7c97acfd1f48957a37870e6763e30c82e322080c`

Desktop fallback ranges are limited to the respective Unicode scripts. Android uses bundled faces for the matching language and language-list row; API 29 and later combine the three faces with the system fallback chain. Earlier supported Android versions use the matching bundled face with their system fallback. Final visual checks must include supplementary-plane characters, Tibetan combining marks, mixed text and font/UI scaling.
