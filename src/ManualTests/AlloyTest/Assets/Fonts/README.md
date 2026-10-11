# Fixed font fixture

Unmodified `NotoSans-Regular.ttf`, Noto Project Authors, SIL Open Font License 1.1.
The complete license is in [OFL.txt](OFL.txt).

- Upstream: https://github.com/notofonts/noto-fonts
- Revision: `ffebf8c1ee449e544955a7e813c54f9b73848eac`
- File: `hinted/ttf/NotoSans/NotoSans-Regular.ttf`
- SHA256: `B85C38ECEA8A7CFB39C24E395A4007474FA5A4FC864F6EE33309EB4948D232D5`

`NotoSansMono-Regular.ttf` from `hinted/ttf/NotoSansMono/` at the same revision
uses the same OFL license. SHA256: `D9E2B23D19F8230BE7146F409A52B1D23117E635E28F2E2892CF91B7382F325B`.
It is the independent alias/monospaced metrics fixture for catalog tests.

`NotoSansSymbols-Regular.ttf` from `hinted/ttf/NotoSansSymbols/` at the same
revision uses the same OFL license. The golden catalog maps `Segoe UI Symbol` to it
for the shared scene's arrow buttons; that alias is explicit, not system fallback.
SHA256: `8F02F31959BBDF6061547A188248E13F84DC5FDD940326EC494675F453F072BB`.

The Settings golden probe maps all other families to Noto Sans. Cyrillic and Latin
text are rendered from the delivered file. Unsupported symbols/emoji use the same
face's missing-glyph symbol; there is no installed-font fallback or emoji shaping.
The interaction script still verifies emoji grapheme editing/selection separately.
The fixture does not promise typography/shaping features absent from the current adapter.
