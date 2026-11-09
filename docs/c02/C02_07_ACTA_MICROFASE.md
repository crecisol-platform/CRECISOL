# CRECISOL — C02-07 · Acta de cierre técnico de diseño móvil y accesibilidad basal

**ESTADO: C02-07 CERRADA TÉCNICAMENTE (7/9 microfases del C02) únicamente tras fusionar esta acta a `dev` y comprobar CI POSTMERGE sobre el SHA final de esa fusión.** La implementación técnica y las evidencias del PR #42 ya fueron verificadas; la presente acta documenta su cierre secuencial. **El capítulo C02 NO CERTIFICADO; C01 es el único capítulo certificado (1/37), C03 bloqueado.** El resultado automatizado de axe **NO** certifica conformidad WCAG completa ni accesibilidad con tecnologías de asistencia.

## 1. Autoridad, alcance, autorización y secuencia

- Repositorio único de lectura/escritura: `solqaryn-platform/CRECISOL`; rama de integración `dev`, rama documental `feature/c02-07-acta-cierre`. No se ha escrito en `main`, `solqaryn/Solqaryn`, producción, VPS, DNS, cobros ni servicios de terceros. Ramas `main` y `dev` no tienen protección automática; revisión manual y PR son controles operativos, no enforcement de GitHub.
- Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`), referenciado por `docs/MASTER_REFERENCE.md`. `qa/c02_01_source.py` verifica los bytes y el capítulo 02 durante CI; no se sustituye por el PDF.
- Precedencia: C01 certificado y C02-01..06 cerradas técnicamente, última acta [C02-06](C02_06_ACTA_MICROFASE.md); `dev` anterior `d54147596984275d98cf7a313a9fdeb8e8b06e22` y sus nueve CI postmerge exitosas, incluida [C02-06 #42651314408](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42651314408).
- El propietario autorizó expresamente continuar con el **cierre de C02-07**. Esto permite documentación, auditoría y PR de esta microfase; **no** representa aceptación anticipada de la certificación integral de C02 ni habilita desarrollar C03.
- Las ocho puertas normativas del Word `MAE-C02-01..08` no equivalen a los nueve entregables técnicos `C02-01..09`. La especificación [C02-07](C02_07_ESPECIFICACION.md) es la fotografía de entrada (menciona 6/9) y se conserva para trazabilidad; este documento y el [plan vigente](../C02_PREPARACION.md) acreditan el nuevo estado solo después del gate documental.

## 2. Entregables reales e impacto acotado

**PR técnico [#42](https://github.com/solqaryn-platform/CRECISOL/pull/42), fusionado en `dev` con SHA exacto `912c709ad51082093a82c615fa29106904639a43` el 9 de noviembre de 2026.** Último HEAD técnico de feature: `6cb492dbb406edc9de2fed756f987351128320ce`.

- Contrato [C02_07_MOBILE_ACCESIBILIDAD.json](C02_07_MOBILE_ACCESIBILIDAD.json) y especificación: seis superficies `UI01..UI06`, seis experiencias `E01..E06`, 18 necesidades `N01..N18`, 16 escenarios adversariales de contrato `NEG01..NEG16`, cuatro anchos de referencia (320/360/768/1280 CSS px), redimensionado del texto al 200 %, objetivos táctiles de al menos 44 x 44 CSS px, teclado/foco, contraste forzado y movimiento reducido.
- `frontend/src/styles.css`; `frontend/src/app/pages/platform-home.component.{ts,html,css}` y `public-information.component.{ts,html,css}`; `not-found.component.ts`: enlaces saltar al contenido con foco real sin remontaje, reflujo de textos públicos largos, foco visible y controles móviles operables. Se conservan solo las cuatro rutas públicas autorizadas (inicio, experiencias, demostraciones y alta de empresas informativa) y la negación wildcard/404.
- `frontend/tests/c02-07.mobile-accessibility.spec.ts` (10 Chromium públicos) y `frontend/tests/c02-07.patterns-mobile.spec.ts` (5 Chromium del laboratorio Angular real), `frontend/playwright.patterns-a11y.config.ts`, con `frontend/tests/pattern-harness/pattern-harness.component.ts`: pruebas de reflujo, zoom, navegación, tabla contenida y modal nativo; harness CI aislado en puerto temporal 4201, verificado fuera del bundle de producción.
- `qa/c02_07_audits.py`: auditorías independientes A/B/C del alcance y **18 mutaciones negativas de contrato rechazadas: 6 funcionales, 8 de seguridad y 4 de arquitectura**. `.github/workflows/c02-07-mobile-accessibility.yml`: build, navegador real, regresión y evidencia reproducible con token de solo lectura.
- Impacto: **frontend/, docs/, qa/ y .github/workflows/**. Sin cambios en `backend/`, `database/`, migraciones, `frontend/src/main.ts`, `frontend/src/app/app.routes.ts`, autenticación privada, API/DTO de negocio, datos de tenants, cobros ni ambientes productivos.
- **Parametrización integral:** la marca pública existente se obtiene de la API de publicación versionada y validada. El control desde panel administrativo de logo, cabecera, pie, textos, temas, iconos, formularios, tablas y variantes por `platform/tenant/branch/user` con preview/auditoría/rollback continúa como **requisito futuro, no implementado**. Ninguna opción administrable puede cambiar RBAC/ABAC, resolución de tenant, propiedad, PII o DSN; no admitir HTML/JS/CSS/SQL arbitrario.

## 3. Evidencia ejecutada en GitHub, por SHA exacto

**PREMERGE:** en el HEAD `6cb492dbb406edc9de2fed756f987351128320ce` el workflow [C02-07 #42658594881](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42658594881) y [Foundation #42658594101](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42658594101) terminaron `SUCCESS`. Otros workflows de PR marcados `skipped` **no se contabilizan como aprobados**. Una ejecución preliminar [#42657768849](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42657768849) terminó `failure` antes de las correcciones del HEAD final; no se reetiqueta retroactivamente como exitosa.

**POSTMERGE del código del PR #42:** consulta de GitHub Actions para `head_sha=912c709ad51082093a82c615fa29106904639a43`, todos con `event=push`, `status=completed`, `conclusion=success`. **10/10 workflows de ese SHA terminaron `SUCCESS`:**

| Workflow | Run de GitHub Actions | Estado |
| --- | --- | --- |
| C02-07 móvil y accesibilidad | [#42659404907](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404907) | SUCCESS |
| C02-06 formularios/tablas | [#42659404570](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404570) | SUCCESS |
| C02-05 estados | [#42659404637](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404637) | SUCCESS |
| C02-04 navegación privada denegada | [#42659404622](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404622) | SUCCESS |
| C02-03 arquitectura pública | [#42659404700](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404700) | SUCCESS |
| C02-02 necesidades | [#42659404529](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404529) | SUCCESS |
| C02-01 fuente y personas | [#42659404957](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404957) | SUCCESS |
| C01-06 identidad visual | [#42659404524](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404524) | SUCCESS |
| C01-08 gate integral | [#42659404591](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404591) | SUCCESS |
| Foundation | [#42659404615](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404615) | SUCCESS |

El **job 129306576686** de C02-07 POSTMERGE muestra pasos `SUCCESS` para validación del Word, regresión de auditorías anteriores, A y B, Node 24.15, `npm ci`, `npm audit --omit=dev --audit-level=high` (0 vulnerabilidades reportadas), Chromium real, typecheck/build Angular 22 de producción, rechazo de harness en bundle, build aislado, compilación ASP.NET Core 10 Release (`0 Error(s)`) y auditoría C. Sus logs muestran inequívocamente **19/19 + 8/8 + 10/10 + 5/5 = 42/42 pruebas Chromium PASS**. El paso de subir diagnósticos `on failure` fue `skipped` por no existir fallo; **no** se cuenta como éxito ejecutado.

**CI del PR documental:** su propio HEAD, fusión y POSTMERGE deben verificarse *después* de crear esta acta. Los diez resultados anteriores avalan el **código**, no acreditan automáticamente una futura fusión documental. Se prohíbe avanzar si falla la nueva CI.

## 4. Triple auditoría y límites explícitos

| Auditoría | Evidencia ejecutada | Resultado |
| --- | --- | --- |
| **A — funcional y trazabilidad** | Word canónico verificado por script, E01–E06/N01–N18, seis superficies UI01–UI06, 16 escenarios NEG y seis mutaciones de fuente rechazadas; pruebas 320/360/768/1280 | **PASS del alcance C02-07** |
| **B — seguridad y aislamiento de superficies** | Foco y navegación pública `fail-closed`, 404 para rutas privadas, sin permisos desde navegador, errores sin marca antigua, XSS/HTML inseguro rechazado, harness fuera de producción y ocho mutaciones P0 rechazadas | **PASS de las superficies públicas/sintéticas** |
| **C — arquitectura, UX y accesibilidad basal** | Entradas Angular separadas, 42 Chromium, 200 % de texto, 44px, teclado/skip, contraste, movimiento reducido, auditoría axe automática WCAG 2.2 AA, build Angular22/.NET10 y cuatro mutaciones rechazadas | **PASS del alcance técnico C02-07** |

**Límites NO aprobados:** WCAG 2.2 AA **no** tiene certificación manual completa; faltan pruebas con NVDA, JAWS, VoiceOver, TalkBack, hardware físico, otros navegadores y locales, y validación de usabilidad asistiva. No se midieron Lighthouse, cargas productivas ni rendimiento privado. Sin backend privado ni datos reales, no se probó autorización real BOLA/IDOR, RBAC/ABAC, MFA, ramas, acceso a PII, aislamiento de colas/archivos/backups o MySQL cross-tenant; estas puertas siguen pendientes de sus capítulos. Tampoco existe runtime de panel de parametrización integral.

**Evaluación de riesgo:** sin fallo P0/P1 conocido **dentro del alcance técnico limitado** después del HEAD y POSTMERGE exitosos; esta frase no atribuye seguridad global ni exime pruebas futuras. El PR documental solo cambia `docs/`; no debe introducir secretos, rutas, servicios, privilegios ni migraciones.

## 5. Puerta de salida, continuidad y reversibilidad

1. Revisar el diff del PR de acta; debe contener exclusivamente este archivo, `docs/C02_PREPARACION.md` y `docs/PLAN_37_CAPITULOS.md`. Verificar ausencia de secretos/datos reales y que el Word/PDF y demás archivos con autoridad permanezcan intactos.
2. Exigir CI del **último HEAD** documental, fusionar a `dev` con SHA esperado y comprobar workflows POSTMERGE del SHA de fusión. Hasta cumplir ambas verificaciones, el recuento oficial permanece condicionado, **no fingir 7/9 certificado**.
3. Cumplidas estas puertas, **C02-07 CERRADA TÉCNICAMENTE (7/9)**, **C02-08** recorridos y contratos de experiencia es la **siguiente microfase habilitable, todavía no iniciada**. **C02-09** auditoría integral y aceptación de capítulo sigue pendiente. **C02 NO CERTIFICADO**, C03 bloqueado; solo C01 certificado (1/37).
4. Reversión por PR de los cambios del PR documental y, si procediera, del PR #42 en `dev`. Sin rollback de DB, production, VPS, DNS ni datos de clientes. `main` y SOLQARYN intactos.

**Acta técnica, no acta de certificación del capítulo 02.** La aceptación humana de C02 deberá solicitarse únicamente al finalizar su microfase 09, con las tres auditorías integrales y evidencia real.
