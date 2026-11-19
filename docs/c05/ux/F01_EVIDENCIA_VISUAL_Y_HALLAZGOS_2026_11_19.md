# CRECISOL · C05 UX F01 — Evidencia visual primaria y hallazgos verificables

**Fecha:** 2026-11-19. **Naturaleza:** lectura y captura de la interfaz actual, SIN ejecutar el rediseño.  
**Base previa al rediseño:** `f61a79d242288c686a88a7c3dd711f2be9a48c18` (rama PR #172).  
**Rama F01:** `feature/c05-ux-f01-inventario-20261119` (PR #174, DRAFT).  
**Ámbitos separados:** cuenta empresarial y operadores de plataforma; no confundir con autorización real del backend.

## Primera evidencia ejecutada (integridad comprobada)

Run de GitHub Actions **44619062318**, SHA F01 **`7ef41465296830b02229e5043aca1088984cd0d9`**, conclusión **SUCCESS**. Artefacto de Actions ID **14575742705**, nombre `c05-f01-synthetic-visual-baseline-7ef41465296830b02229e5043aca1088984cd0d9`, creado a las 2026-11-19T21:33:31Z.

La inspección descargó y abrió un ZIP real con **424 archivos**: **212 capturas PNG** y **212 documentos JSON**. Las 212 parejas contienen SHA de fuente `f61a79d...`, `synthetic=true`, ruta y viewport. Hay exactamente **53 PNG en 320 px, 53 en 390 px, 53 en 768 px y 53 en 1440 px**. No aparecen rutas inseguras dentro del ZIP ni imágenes sin JSON. `bodyScrollX` es 0 en las 212 mediciones. `dialogsOpen=1` aparece en 108 capturas (27 modales × 4 viewports), y 104 son vistas sin modal. Estos datos verifican la **existencia, identidad y composición de capturas sintéticas**, no la autorización de servidor ni una nota premium.

URL de la ejecución: https://github.com/crecisol-platform/CRECISOL/actions/runs/44619062318. URL de artefactos del run: https://github.com/crecisol-platform/CRECISOL/actions/runs/44619062318/artifacts.

## Hallazgos observados directamente en los PNG de la base

**UX-F01-001 — Cabecera a 320 px (severidad visual P2):** la palabra de marca `CRECISOL` puede partirse al final de la línea. Un `skip-link` aparece superpuesto al contenido de la consola en la captura de recuperación de operador 320 px. El conteo de desbordamiento horizontal igual a 0 no implica que no exista solapamiento o ruptura del texto.

**UX-F01-002 — Diálogos extensos (P2 visual/evidencia):** en la recuperación de operador y en el formulario de política de sesión, el pie y algunos campos están fuera del primer viewport de 320 px; el diálogo requiere scroll propio. La captura original `fullPage` no recoge necesariamente el final del scroll interno. Debe capturarse la vista inferior y comprobar retorno de foco, cierre y acceso al CTA. No se modifica la UI en F01.

**UX-F01-003 — Técnica de captura de modal (P2 de evidencia):** `page.screenshot({ fullPage: true })` al abrir un diálogo puede mostrar el resto del documento fuera del backdrop, lo que no equivale a una vista humana de 900 px. La prueba F01 ahora registra captura de viewport para diálogo y `fullPage` solo para páginas normales; además registra geometría y posición del pie del diálogo.

**UX-F01-004 — Jerarquía y densidad (deuda de rediseño F02+):** en las vistas revisadas se repite `Sesión verificada` como gran título entre pestañas, la mayoría de acciones se presentan como filas/botones uniformes, y se observan vacíos verticales considerables en escritorio. Los datos de sesión/historial se muestran como valores técnicos sin resumen visual prioritario. Esto es evidencia suficiente para rechazar **9/10 en la línea base**, pero no sustituye una calificación de cada estado.

**UX-F01-005 — Mensajería genérica (deuda de rediseño F02+):** el código contiene confirmaciones genéricas `Solicitud procesada` para operaciones de seguridad distintas; el inventario de texto `F01_MENSAJES_Y_FLUJOS.md` preserva las frases exactas. Las diferencias entre recepción neutra, éxito verificable, fallo y sesión revocada deben mantenerse en las futuras mejoras.

**UX-F01-006 — Alcance real de los mocks (riesgo de auditoría, no hallazgo de backend):** las capturas reproducen estados con señales de Angular, respuestas sintéticas y QR/semillas ficticias. La fuente de screenshot no comprueba por sí sola 401/403 reales, aislamiento tenant/sucursal, expiración a los 30 minutos o revocación MySQL. No atribuir esos controles al pipeline visual.

## Contraste externo dentro de la misma base congelada

Los workflows existentes de PR #172 se consultaron **sobre el SHA exacto `f61a79d...`**:
- Run **44601678730**, `C05 operational identity Angular API MySQL`: jobs `operational` y `regressions` concluyeron SUCCESS.
- Run **44601679085**, `C05 institutional modals A-B-C UX security`: A/B/C/D y gate concluyeron SUCCESS.
- Run **44601678999**, `C05-08 MFA durable step-up recovery HTTP MySQL8.4`: job integrado concluyó SUCCESS.

Estos resultados son regresiones históricas del código base, **no ejecutadas por la captura F01**. No reemplazan la inspección visual, las pruebas de permiso con datos reales o la aceptación del propietario.

## Ampliación de cobertura F01

La prueba Playwright de F01 fue extendida **solo dentro de `frontend/tests/`** para incluir desafío MFA cuenta/plataforma, QR TOTP y claves **ficticias**, respaldo ficticio, error y expiración simulada de operador, monitor/sesiones vacíos, política global sin permiso de editar y tomas del pie interno de modales largos.

El validador adicional `qa/c05_ux_f01_evidence_audits.py` exige pares PNG/JSON reales, `runnerSha`, `sourceSha`, 4 tamaños, 27 modales por tamaño, estados MFA/error/vacío/expiración y evidencia de scroll. Este verificador tampoco reemplaza controles de backend.

La ampliación se entenderá **ejecutada** solo si un nuevo run termina SUCCESS, entrega los PNG y supera la inspección de artefacto; hasta entonces no se declara cobertura aprobada.

## Política de calificación y cierre

- Promedio heurístico **4,29/10** de **31** muestras referenciadas por el propietario; las otras **34** filas se preservan inicialmente como *sin nota independiente* por ausencia de captura verificada en la primera muestra. Es una media parcial, no una calificación global 65/65.
- F01 puede documentar hallazgos negativos; no se corrigen visualmente ni se suavizan para declarar 9/10. Ese es el objetivo exigido al diseño posterior.
- Certificar **únicamente la integridad del inventario y de una línea base reproducible** si A/B/C cumplen evidencia. No afirmar que con ello se certifican la seguridad productiva, usabilidad humana completa ni la nota 9/10.
- No fusionar PR #173, no modificar backend/database, no escribir en SOLQARYN ni avanzar a F02 sin autorización y gate.
