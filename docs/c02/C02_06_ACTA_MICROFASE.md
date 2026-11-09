# CRECISOL — C02-06 · Acta de cierre técnico de patrones de formularios y tablas UX

**ESTADO: C02-06 CERRADA TÉCNICAMENTE (6/9 microfases C02), condicionado al merge, CI y verificación POSTMERGE de la presente acta.** El cierre acredita patrones de UX y un laboratorio de pruebas aislado, **NO** implementación privada ni autorización backend. **Capítulo 02 NO CERTIFICADO; únicamente C01 certificado (1/37 capítulos), C03 BLOQUEADO.** C02-07 — diseño móvil y accesibilidad basal — siguiente microfase habilitable, **NO iniciada**.

## 1. Autoridad, secuencia y procedencia

- Repositorio único autorizado `solqaryn-platform/CRECISOL`, rama de desarrollo `dev`; cambios por PR en `feature/c02-06-*`. Ninguna escritura en `main`, `solqaryn/Solqaryn`, sus skills, VPS, DNS, producción, pagos o servicios externos.
- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, huella SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Fuente y capítulo 02 comprobados por lectura real de Word en `qa/c02_01_source.py`: P01–P11, seis contextos de experiencia E01–E06, 18 necesidades N01–N18. Las ocho puertas normativas `MAE-C02-01..08` **NO** son las nueve microfases técnicas `C02-01..09`. Login sin selector de empresa prevalece; permisos siempre backend.
- **Puertas previas:** C01 certificado y C02-01..05 cerradas técnicamente. [Acta C02-05](C02_05_ACTA_MICROFASE.md), PR #38/#39, `dev` SHA `cf751709f4cd5dac5e579dccc50c060bf20ae396` y ocho CI POSTMERGE SUCCESS, incluidas [C02-05 #42641529918](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641529918), [C01-07 #42641530194](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641530194) y [C01-08 #42641529853](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641529853).
- El propietario autorizó continuar C02-06 expresamente con «ADELANTE CONTINUA POR FAVOR». Esa instrucción permite desarrollo, pruebas y cierre técnico secuencial, no certificación anticipada de C02 o apertura C03.

## 2. Entregables exactos e impacto controlado

**PR técnico [#40](https://github.com/solqaryn-platform/CRECISOL/pull/40)** fusionado en `dev` SHA exacto **`d82cfc0a57561f21266a1f3e2e8c328dd3c4aeb4`**.

- [Contrato C02-06](C02_06_PATRONES_FORM_TABLA.json) y [especificación](C02_06_ESPECIFICACION.md): **cuatro patrones FP01/FP02/TP01/TP02**, 14 escenarios NEG01–NEG14, seis experiencias E01–E06 y N01–N18, requisitos de seguridad y configuración futura versionada. No se alteró el maestro.
- `frontend/src/app/ui/patterns/pattern-contract.ts`: modelo tipado para etiquetas, ayudas, tokens/longitudes, clave y columnas dentro de límites explícitos; validador de UX puro **sin autorización ni persistencia**.
- `draft-form-pattern.component.{ts,html,css}`: Angular 22 standalone OnPush; campo etiquetado con ayuda y `aria-invalid`, teclado, feedback local `role=alert`, botón de revisión sin envío, señal de edición sucia, `beforeunload` y diálogo nativo cancelar/Escape/confirmar descarte. El host confirma los cambios de conjunto/schema **antes** de cambiar el contexto; el componente limpia borrador viejo cuando recibe nuevo ámbito.
- `table-pattern.component.{ts,html,css}`: tabla de solo lectura con `caption/th/td`, `aria-sort`, filtro local, ordenación estable, scroll horizontal desde 320 px, limpieza del filtro/orden al cambiar contexto. El host debe suministrar **filas ya autorizadas y frescas**; el patrón NO consulta backend ni prueba acceso a PII.
- `frontend/tests/pattern-harness/`, `tsconfig.patterns.json`, `playwright.patterns.config.ts`: aplicación Angular de prueba **separada**, solo accesible mediante `ng serve --configuration pattern-harness` en puerto temporal 4201. `frontend/angular.json` conserva `production` con `src/main.ts` original; el harness usa entrada y salida distintas, sin nueva ruta pública o privada, y se verifica que su texto/selector no existe en el bundle de producción.
- `frontend/tests/c02-06.form-table.spec.ts`: **ocho pruebas Playwright reales contra componentes Angular montados**, con datos sintéticos A2/A10/B1, teclado, modal/Escape, confirmación de cambios pendientes, errores de campo, XSS sin renderizado HTML/SVG, orden/filtro, cambio de contexto, móvil 320px y axe WCAG 2.2 AA automatizado. Se repitieron **19 pruebas Chromium** de C02-03/04/05, total **27/27**.
- `qa/c02_06_audits.py` y workflow `.github/workflows/c02-06-form-table-patterns.yml`: **A/B/C**, **18 mutaciones adversariales** de contrato (6 fuente, 8 seguridad, 4 arquitectura), regresiones de C02-01..05, Word SHA, build/typecheck Angular22, build aislado y producción, npm audit, .NET10 Release y negación de harness en el bundle público.

**Impacto arquitectónico:** cambios únicamente en `frontend/`, `docs/`, `qa/` y CI. `app.routes.ts` conserva las cuatro rutas públicas de C02-03 + wildcard; `src/main.ts` de producción sin alteración; **backend/ y database/ sin cambios** (ninguna API/DTO, RBAC, servicio, tabla, migración o DSN). Sin login, alta real, compras, cobros, archivos privados ni datos de tenants. La configuración administrativa de formularios y tablas es **contrato futuro**, todavía no runtime implementado.

## 3. Evidencias GitHub reales, incluidos fallos preliminares

**PREMERGE: último HEAD técnico `8f319ef1ea98008d398e40be80a134b30993685e`:**
- [C02-06 #42648868339](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42648868339) **SUCCESS**: 27/27 Chromium = 19/19 regresiones + 8/8 harness; A/B/C PASS con 18 mutaciones negativas, tipos/build Angular22 producción+harness, auditoría dependencias, exclusión de harness de bundle y compilación ASP.NET10 Release.
- [Foundation #42648868417](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42648868417) **SUCCESS**: rama/alcance, integridad y secretos.
- **Cuatro ejecuciones preliminares FALLIDAS reconocidas:** [#42647880301](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42647880301) (escenario obligatorio NEG02 corto); [#42648047112](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42648047112) (traza NEG01–NEG14 incompleta en especificación); [#42648191846](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42648191846) (falso positivo textual por comentario); [#42648357487](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42648357487) (7/8 nuevos Chromium; selector XSS examinaba scripts legítimos del framework). Se corrigieron sin quitar pruebas ni reducir los controles. La negativa final se limita al DOM del componente editable y **sí** verifica que no se generó HTML inyectado.

**POSTMERGE del código: las siguientes ocho acciones GitHub para SHA `d82cfc0a57561f21266a1f3e2e8c328dd3c4aeb4` fueron consultadas con `event=push`, `status=completed`, `conclusion=success`:**

| Workflow | Resultado |
| --- | --- |
| [C02-06 #42649577691](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577691) | **SUCCESS**, 27/27 Chromium, A/B/C, build producción+harness y .NET10 |
| [C02-05 #42649577850](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577850) | **SUCCESS**, estados públicos y 19/19 regresiones |
| [C02-04 #42649577851](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577851) | **SUCCESS**, rutas privadas propuestas siguen inaccesibles |
| [C02-03 #42649577513](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577513) | **SUCCESS**, sitemap público sin nuevas rutas |
| [C02-02 #42649577478](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577478) | **SUCCESS**, necesidades y personas |
| [C02-01 #42649577726](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577726) | **SUCCESS**, Word maestro y fuente |
| [Foundation #42649577757](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577757) | **SUCCESS**, alcance y secretos |
| [C01-06 #42649577427](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42649577427) | **SUCCESS**, identidad oficial, assets y regresión responsive |

**El PR documental de esta acta**, cuando esté integrado, requiere CI en último HEAD, merge con `expected_head_sha` y verificación de **sus propios workflows POSTMERGE**. No se atribuye aprobación del PR documental antes de que esas puertas finalicen.

## 4. Tres auditorías y límites de lo probado

| Auditoría | Qué se comprobó efectivamente | Resultado |
| --- | --- | --- |
| **A — funcional/trazabilidad** | Word SHA, 6 experiencias/N01–18, cuatro patrones, 14 negativos, 6 mutaciones de ID/origen/función/requisito | **PASS** |
| **B — seguridad** | Sin HTTP ni escritura en componentes; XSS/errores/confirmaciones, no ejecutar HTML, borrador protegido y contexto reiniciado; 8 mutaciones de tenancy/PII/rol/esquema | **PASS del alcance UI y harness** |
| **C — arquitectura/UX** | Distintas entradas build producción/harness, 320px, foco/teclado, modal, WCAG automatizado, compilación Angular22/.NET10 y exclusión de test-harness del bundle; 4 mutaciones | **PASS del alcance técnico C02-06** |

**Límites y riesgos pendientes:** ningún backend privado implementado para probar BOLA/IDOR cross-tenant, autorización de filas recibidas, autenticación/MFA, CSRF, idempotencia, auditoría real, guarda de navegación privada, aprobación de consentimientos o persistencia de formularios de negocio. Tampoco existe sistema administrativo runtime que permita editar los campos vía panel; se definieron límites y contrato para su construcción futura. La muestra sintética no reemplaza pruebas de rendimiento de tablas con datos reales. Estas capacidades deberán pasar sus propias pruebas y tres auditorías al implementarse en C04–C34. No se afirma que 27/27 pruebas de frontend certifiquen el SaaS multitenant.

**No existen P0/P1 conocidos abiertos del alcance aislado tras CI final**, sin pretender ausencia de riesgos en funciones privadas aún no construidas. Revisión de diff/secretos: 20 archivos autorizados; ningún cambio a SOLQARYN, producción o main.

## 5. Decisión y continuidad

**C02-06 CERRADA TÉCNICAMENTE (6/9)** solo después de integrar **esta acta** y registrar su CI postmerge. El capítulo C02 sigue **NO CERTIFICADO** (1/37 capítulos certificados, exclusivamente C01), C03 bloqueado. **C02-07** móvil/accesibilidad basal será la siguiente microfase habilitable, **no iniciada**.

**Reversión:** revert por PR del código C02-06 #40 y del PR documental de esta acta en `dev`, sin migraciones ni datos reales, sin rollback de producción. La documentación mantiene historia de commits, CI positivas/negativas y dependencias; nunca se avanza por atajo si falta un gate.
