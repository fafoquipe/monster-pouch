# Pixelify Sans

Fuente para la interfaz de Monster Pouch, obtenida del repositorio oficial de Google Fonts el 11 de septiembre de 2026. No requiere instalar fuentes en Windows ni conexión al iniciar el juego.

- Autor: Stefie Justprince / The Pixelify Sans Project Authors.
- Archivo original: `PixelifySans[wght].ttf`, fuente variable con eje de peso 400–700.
- Archivo fuente original, SHA-256: `9BA86CD010A4DE309D263CEFF8E8044092C9DB7EFDA869620CB9FF1C4389E8A5`.
- Derivado incluido: **Monster Pouch Pixel**, `Assets/Resources/MonsterPouch/PixelFont.ttf`.
- SHA-256 del derivado: `E6F522BE7DB60B79C3EF3376498E545966B9872DF4FBBAB45C116441D08ED74F`.
- [Fuente oficial en Google Fonts](https://github.com/google/fonts/tree/main/ofl/pixelifysans).
- [Archivo descargado](https://raw.githubusercontent.com/google/fonts/main/ofl/pixelifysans/PixelifySans%5Bwght%5D.ttf).
- [Proyecto original del autor](https://github.com/eifetx/Pixelify-Sans).
- Licencia: SIL Open Font License 1.1. Texto completo conservado en `PixelifySans-OFL.txt` y como recurso `Assets/Resources/MonsterPouch/PixelFont-OFL.txt` para incluirlo en la distribución local.

El derivado se genera con `tools/build-pixel-font.py` y fontTools 4.65.0 a partir del TTF original. Se fija el peso regular original en 400 para facilitar la importación en Unity y se añaden seis dibujos vectoriales originales sobre cuadrícula de nueve filas: ★ (U+2605), ☆ (U+2606), ◆ (U+25C6), ✦ (U+2726), ↻ (U+21BB) y Ⅱ (U+2161). Sus nombres internos de familia se cambian a Monster Pouch Pixel; se conservan los créditos originales, el copyright y la licencia OFL 1.1. Las seis adiciones y este derivado también se distribuyen bajo OFL 1.1.

Se compararon los contornos y métricas de letras y cifras del derivado con la instancia original de peso 400: permanecen idénticos. Se verificó la cobertura de vocales acentuadas, diéresis, ñ, signos de apertura españoles y los seis símbolos. Las flechas ← ↑ → ↓ no forman parte de esta versión; la interfaz actual no las requiere. No se instala ninguna fuente en el sistema operativo.

Para regenerar: instalar `fonttools==4.65.0` en un entorno de herramientas, descargar el archivo original enlazado arriba y ejecutar `python tools/build-pixel-font.py --source RUTA_AL_TTF_ORIGINAL`. El script comprueba el hash de entrada, añade los símbolos y valida los caracteres necesarios antes de finalizar.
