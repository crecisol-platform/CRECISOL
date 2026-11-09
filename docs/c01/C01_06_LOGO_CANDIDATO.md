# C01-06 — Verificación del candidato al logotipo oficial

**Estado: EN DESARROLLO; NO APROBADO**, sin publicar ni instalar gráfico en frontend. C01: **5/8 microfases cerradas**; **0/37 capítulos certificados**. C01-07 NO habilitada.

## Fuente, alcance y trazabilidad

Autoridad exclusiva: docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e, Git blob 214bf9c3d9763be23a77b7e0c464359584edde46. Requisitos: **P14**, **P25**, **T02-R01**, **T03-R01** (identidad, medios/favicons y aceptación de marca), **P07** (configuración como datos seguros), **P04/P06** (tenant y autorización por servidor). C01-05 cerró en PR #20, merge b902193b954d29f22dbdf8da9b22e48680f45dcd y CI posterior #42558999820.

**Alcance de esta preparación:** extracción byte a byte del único vínculo gráfico incrustado en el Word, comprobación de procedencia y casos adversariales. Sin redibujar, cambiar color, proporciones ni retocar la imagen; sin cambiar frontend, backend, database, DTO, API, auth, permisos, redes ni activos publicados.

## Evidencia de candidato (no aprobación)

qa/c01_06_logo_candidate.py verifica el SHA-256 real del Word en GitHub Actions antes de extraer, relación interna OOXML, tipo PNG/JPEG no ejecutable, dimensiones, integridad de chunks PNG y SHA-256 de la imagen. Se publica **solo un artefacto privado de GitHub Actions, denominado C01-06-candidate-NOT-APPROVED**, retenido tres días. El archivo derivado se copia sin transformación y NO se añade a la aplicación. Si el Word no expone una relación única segura, la prueba falla, no se inventa logotipo alternativo.

Pruebas positivas: PNG sintético de control, DOCX sintético único, fuente canónica con hash verificado, extracción de bytes idénticos. Negativas: digest alterado, relación externa, ruta traversal, HTML/JS disfrazado como PNG, múltiples identificadores de imagen, PNG con CRC corrupto. La suite rechaza esos casos, sin convertir fixtures sintéticos en marca.

Auditoría A preliminar: origen y matriz de requisitos. Auditoría B preliminar: rechazo de medios externos/maliciosos y ausencia de divulgación por este script; autorización backend, BOLA/IDOR y tenant end-to-end no están implementadas aquí. Auditoría C preliminar: dimensiones y byte fidelity; no equivale a aceptación visual.

## Puertas pendientes — no cerrar C01-06 sin ellas

1. Comparación visual humana con portada del Word y aprobación explícita del **archivo gráfico independiente** oficial, con SHA-256.
2. Incorporación segura del logo aprobado a frontend y favicon si procede, sin deformaciones, con texto alternativo y separación de branding de plataforma/tenant.
3. Build real Angular 22 y Node 24, pruebas Chromium móviles/escritorio, fidelidad visual, fallo del recurso, WCAG 2.2 AA, caché, tamaños y rendimiento.
4. Auditorías completas A/B/C, cero P0/P1 abiertos, acta de cierre, CI del último HEAD y CI posterior al merge a dev.

Esas pruebas de Chromium, WCAG y aceptación visual final permanecen **NO EJECUTADAS** en esta preparación. El PASS de pruebas de extracción no equivale al cierre de C01-06. Sin la aprobación y pruebas requeridas, no se inicia C01-07.

## Riesgos, reversión y restricciones

Riesgo: un candidato técnicamente auténtico del Word no constituye automáticamente un activo apto/aprobado para web. No suponer derechos registrales, aprobación de emisor o DNS. Revertir PR elimina únicamente documentación, QA y workflow; el candidato efímero vence tras tres días. No toca main, SOLQARYN, producción, VPS, R2, servicios pagos ni secretos.


## Acta parcial de evidencia CI — candidato exclusivamente (sin cierre)

Fecha de la comprobación: 2026-11-08 en Honduras / 2026-11-09 05:30 UTC. **CI realmente ejecutada** sobre HEAD ec5a3b379483f915215dfc5c330e34a2dbdcce9f:

- [C01-06 GitHub Actions #42567719986](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42567719986): SUCCESS; job de extracción + seis negativas únicas + revisiones preliminares A/B/C + upload de candidato: SUCCESS.
- [Foundation #42567719759](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42567719759): SUCCESS, mismo SHA.
- Fuente Word SHA-256 realmente recalculado en CI: e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e, equivalente al valor canónico documentado.
- Relación OOXML hallada: word/media/image1.png. PNG **1298 × 859 píxeles, 1 054 246 bytes**. SHA-256 exacto de los bytes: **9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f**. Extracción byte a byte sin modificar formato.
- Artefacto privado GitHub Actions ID **13652032125**, denominado C01-06-candidate-NOT-APPROVED, retenido tres días. Ubicación de consulta: página de la ejecución de CI, sección Artifacts. No equivale a aprobación oficial.
- Positiva sintética de documento/PNG y lectura del Word real PASS. Negativas únicas PASS: SHA alterado, relación externa, traversal, HTML disfrazado, múltiples imágenes y CRC PNG corrupto.
- Auditoría **A preliminar PASS** (autoridad/matriz); **B preliminar PASS** (rechazo de medios peligrosos); **C preliminar PASS** (metadatos/dimensiones). **NO** son auditorías finales de C01-06.
- Incidencia no bloqueante del job de upload: advertencia de deprecación Node DEP0169 sobre url.parse en la acción, sin fallo observado. Debe revisarse al actualizar acciones; no se omitió ni silenció una prueba.
- **PENDIENTE / NO EJECUTADO:** cotejo visual humano con portada, aprobación expresa del gráfico individual, instalación en frontend, build Angular/Node, Chromium/WCAG de logo real, auditorías completas, CI final y post-merge. La microfase NO se cierra; 5/8, 0/37.
