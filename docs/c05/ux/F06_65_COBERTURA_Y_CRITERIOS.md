# CRECISOL · F06 — Puerta exhaustiva de 65 superficies F01 (21-11-2026)

## Fuente y decisión
Inventario original **65 identificadores** (17 vistas, 18 estados, 3 complementos, 27 modales) del PR #174, ruta original `docs/c05/ux/F01_MATRIZ_INTERFACES_BASE.csv` en `feature/c05-ux-f01-inventario-20261119`. Este archivo de control **no sustituye** F01 y no altera su historia. La matriz derivada `F06_65_SUPERFICIES_VALORACION.csv` tiene todas las filas 001–065, con nota y aceptación comercial en blanco hasta que exista evidencia visual de ese SHA.

## Instrumentación
Se trasladó conceptualmente el capturador original de F01 y se adaptó a **F06 HEAD actual**, sin importar assets, credenciales, datos personales ni dependencias de otro proyecto. El Playwright `c05-f06-inventory-65.spec.ts` fotografía escenarios sintéticos a **320×720, 390×844, 768×1024, 1024×768 y 1440×900**. Cada PNG incluye JSON propio con ID(s) de F01, SHA de GitHub, viewport, tipo y ámbito; los diálogos se capturan abiertos y cuando tienen scroll interno también con el pie desplazado. El auditor `qa/c05_ux_f06_65_coverage.py` exige por cada ancho los 65 IDs y ningún desbordamiento; reporta explícitamente todos los faltantes. **Si algún caso no está disponible o no tiene evidencia, FALLA; no se declara cobertura.**

### Rúbrica editorial no automática
Puntuar individualmente 0–2 por: (1) jerarquía/CTA, (2) composición y densidad, (3) consistencia institucional, (4) lenguaje y estados, (5) responsive/accesibilidad visual. El total 0–10 se consigna solo tras abrir el PNG completo, comprobar estado con datos y vacío, operación, teclado y seguridad backend. **Mínimo 9.5/10 en cada fila, sin compensación de promedio ni redondeo.** Un test verde no equivale a 9.5 editorial. Si falta hardware/lector real, declarar limitación sin fabricar prueba.

### Seguridad y alcance
Esto es validación de **rendering sintético**, no verifica permisos, MFA, CSRF, passkey WebAuthn ni aislamiento multitenant. Requiere pasar además la batería E2E real Angular→.NET10→MySQL8.4 y auditorías A/B/C sobre el **SHA final**; preservar 30 min inactividad configurables por administrador, protección de QR/respaldos, sesiones y flujos de doble aprobación. Ninguna modificación a backend, database, producción, `dev`, `main` ni SOLQARYN. F07 **NO HABILITADA** hasta 65 dictámenes ≥9.5 con evidencias.

Referencias originales revisadas solo en lectura: Impeccable EXT-03 `pbakaus/impeccable@2149fcce39a90bb409df5f16515f316a76dc6199`, Angular Developer EXT-10 `angular/angular@6808966993bf2b2d455d9b6245e960a59a01b9a9`, Motion EXT-04 `emilkowalski/skills@d23d7f88a2e21c9e4b1418c7abe420f5c1052ba7`. Ninguna skill externa fue instalada ni ejecutada.

## Ronda visual posterior al inventario completo
El workflow [#44941735080](https://github.com/crecisol-platform/CRECISOL/actions/runs/44941735080) obtuvo PASS sobre `1f1dd2b7b7b3caefd1194555d61072c0aebeb93d`: 30/30 pruebas y 390 capturas PNG, con 65/65 IDs visibles en 320, 390, 768, 1024 y 1440. Esto demuestra **cobertura sintética**, no compra 9.5 ni autorización de servidor del nuevo SHA.

La inspección de los PNG originales a 320 px descubrió falta de una indicación visual de continuación en diálogos protegidos largos. La mejora añade un aviso solo cuando realmente queda contenido por desplazar; desaparece al llegar al final y no elimina ninguna instrucción de seguridad. En la cuenta móvil se elimina la tarjeta decorativa anidada del encabezado, conservando título, descripción y navegación. Se incorpora también la vista separada de **Historial**, creada en F03 después del inventario original F01.

El guard de valoraciones permite futuras notas solo si contienen justificación, evidencia visual, comprobación de privacidad y decisión individual coherente con ≥9.5, sin otorgarlas automáticamente. Cualquier cambio exige repetir QA sobre el SHA final. F07 permanece bloqueada.

## Control de alcance A/B/C mantenido (21-11-2026)
El primer workflow #44942437729 del SHA `9e67ef6e` quedó **FAIL de gobernanza** porque el auditor F06 no reconocía los nuevos archivos de cobertura F01; no fue un resultado de seguridad funcional. Se actualizó únicamente el conjunto cerrado `ALLOWED` de `qa/c05_ux_f06_feedback_audits.py`, añadiendo las rutas exactas de evidencia, Playwright, modal CSS y documentos nuevos. **No se modificaron las reglas de auditoría A/B/C, la protección multitenant ni los tests negativos**. Se requiere un nuevo workflow PASS del SHA final antes de cualquier valoración de compra.
