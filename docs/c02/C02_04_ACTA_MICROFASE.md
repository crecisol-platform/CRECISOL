# CRECISOL — C02-04 · Acta de cierre técnico de navegación privada propuesta

**ESTADO: C02-04 CERRADA TÉCNICAMENTE (4/9 microfases C02).** El entregable propio de C02-04 es un **DISEÑO NO OPERATIVO** con auditorías de contrato y pruebas reales del 404; **NO** implementa los permisos ni los paneles futuros. La formalización del presente cierre exige todavía el CI/merge/CI POSTMERGE de este PR documental. **Capítulo C02 NO CERTIFICADO; únicamente C01 certificado (1/37), C03 BLOQUEADO.**

## 1. Autoridad y secuencia comprobada

- Repositorio único autorizado: `solqaryn-platform/CRECISOL`, integración en `dev` y ramas `feature/c02-04-*`. Ninguna escritura a `main`, `solqaryn/Solqaryn`, VPS, DNS, producción ni servicios de cobro.
- Fuente única: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Se leyó y verificó mediante extractor QA de Word con 11 párrafos originales C02, seis filas de personas/contextos T01-R01..R06, precedencia sin selector de empresa en login, flujo P03–P09 y anexos. Separar ocho puertas normativas `MAE-C02-01..08` de nueve microfases operativas `C02-01..09`.
- Dependencias previas: C01 certificado; C02-01, C02-02 y C02-03 cerradas técnicamente. [Acta C02-03](C02_03_ACTA_MICROFASE.md); SHA `e6329e874ae44721dda0bfa2eb9a30acfaf0b990` con seis workflows postmerge SUCCESS (incluido [#42626138512](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42626138512)).
- Autorización del propietario el 9 noviembre de 2026: continuar «bajo el mismo plan, bajo las mismas reglas, sin omitir nada» con la siguiente microfase. El pedido habilita **trabajo técnico C02-04** y su acta después de pruebas, pero **no** certificación global C02 ni apertura anticipada de C03.

## 2. Entregables exactos y límite de la implementación

**PR técnico [#36](https://github.com/solqaryn-platform/CRECISOL/pull/36)** integrado en `dev`, SHA exacto `ecb7807e79427d5059c8b5f4ee0c5dd2195b7cb5`.

- [Especificación de navegación privada](C02_04_ESPECIFICACION.md) + [contrato JSON de diseño](C02_04_NAVEGACION_PRIVADA.json): **cuatro contextos E03–E06**, **12 entradas NAV01–NAV12** ligadas a N07–N18 y fuente P/T original; criterios Given/When/Then, path propuesto **NO RUTEABLE**, dependencia futura, P0, controles de autorización backend y **12 riesgos DEN01–DEN12**.
- SuperAdmin global (PV01): datos agregados por defecto; acceso excepcional a privados requiere MFA/step-up, motivo, expiración, revocación y auditoría. Propietario (PV02): marca/negocio/sucursales **solo autorizados**; empleado (PV03): tareas y expediente por sucursal/asignación y cronología inmutable; consumidor (PV04): recursos propios con N16 como enlace **público publicado** futuro, jamás permiso implícito de expediente.
- Parametrización futura segura: etiquetas, iconos allowlist, orden, grupo, visibilidad, idioma, ayuda y página preferida, administrables por plataforma/tenant/sucursal/usuario con aprobación, versión, previsualización, auditoría y rollback. Matriz de permisos administrada **por backend con mínimo privilegio y reglas inalterables de aislamiento**; campos de UI, guards o parámetros de navegador no autorizan, no ejecutan HTML/JS/SQL y no seleccionan DSN.
- `qa/c02_04_audits.py`: auditorías A/B/C y **18 mutaciones negativas** del contrato (5 trazabilidad, 9 seguridad, 4 parametrización/arquitectura).
- `frontend/tests/c02-04.private-paths.spec.ts`: cuatro pruebas Chromium nuevas, además de ocho de C02-03, que verifican acceso directo a las doce rutas futuras, manipulación de tenant/rol/claims de URL, ausencia de enlaces/formularios privados y 404 accesible a 320px con WCAG 2.2 AA automatizado.
- `.github/workflows/c02-04-private-navigation.yml`: workflow de token read-only y CI con Word íntegro, regresiones A/B/C C02-01..03, Angular 22/Node 24, .NET 10 y navegador. El avance en `docs/C02_PREPARACION.md` permaneció **3/9** hasta documentar esta acta.

**Impacto demostrado del PR #36:** solo `docs/`, `qa/`, `.github/workflows/` y un fichero en `frontend/tests/`. **`frontend/src/`, `backend/`, `database/` sin cambios**: ningún `app.routes.ts`, JWT, permiso RBAC/ABAC, API/DTO, DSN, migración, tenant real, backend privado, alta, cobro o despliegue. Las rutas públicas de C02-03 permanecen intactas y la ruta wildcard sigue usando `NotFoundComponent`.

## 3. Evidencia real y resultados de las tres auditorías

**PREMERGE, HEAD técnico `66566aa8c68bfcb4f7b750b2ce5bb6ed979bbbb4`:**

- [C02-04 #42629980883](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42629980883): **SUCCESS**, 12/12 Playwright Chromium (8 previas + 4 nuevas), A/B/C PASS y **18 mutaciones adversariales rechazadas**, build/tipos/assets Angular22, .NET10 Release, auditoría de dependencias.
- [Foundation #42629980810](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42629980810): **SUCCESS**.
- **Fallos preliminares reconocidos y corregidos:** [#42629874780](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42629874780) FAIL en A por expresión regular que omitía el guion del prefijo legítimo `/mi-cuenta`. Corregido en `66566aa8`, manteniendo allowlist y sin omitir ninguna prueba.

**POSTMERGE del código, evento `push` sobre SHA exacto `ecb7807e79427d5059c8b5f4ee0c5dd2195b7cb5`:**

| Workflow | Resultado GitHub Actions |
| --- | --- |
| [C02-04 #42630487933](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42630487933) | **SUCCESS** — contrato, negativos Chromium, A/B/C, Angular/.NET |
| [C02-03 #42630487964](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42630487964) | **SUCCESS** — rutas públicas, negativos y WCAG |
| [C02-02 #42630487996](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42630487996) | **SUCCESS** — necesidades y auditorías |
| [C02-01 #42630487889](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42630487889) | **SUCCESS** — fuente Word, personas, controles |
| [Foundation #42630487985](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42630487985) | **SUCCESS** — alcance y secretos |

En todos los casos `head_sha` coincidió con el commit técnico, `status=completed`, `conclusion=success`. Se revisaron diff, alcance, secretos y estabilidad de `dev` antes de la fusión.

| Auditoría | Qué se verificó efectivamente | Resultado |
| --- | --- | --- |
| **A — funcional / trazabilidad** | Word original SHA, E03–E06/N07–N18, 4 contextos, 12 tareas, fuentes y dependencias, 5 mutaciones de persona/fuente/ruta/ID/omisión | **PASS** |
| **B — seguridad** | P0 en diseño por cada tarea, backend como única autoridad, sin selector de empresa, acceso global excepcional, pertenencia y ownership, 9 mutaciones negativas + Chromium real negando rutas inexistentes | **PASS**, limitado al contrato y al 404 público |
| **C — arquitectura / UX** | Sin rutas privadas, 4/12 estructura parametrizable, configuración versionada, 4 mutaciones, build Angular/.NET, WCAG automatizado y 320px, regresiones C02-01..03 | **PASS** del alcance C02-04 |

**Pruebas NO ejecutadas y no suplantadas:** BOLA/IDOR cross-tenant en endpoints privados inexistentes, JWT/MFA/step-up reales, permisos efectivos por sesión/sucursal, revocación, MySQL shared/dedicated privado, host/domain tenant reales, datos reales de pago, auditoría de logs privados y benchmark de panel. Cada una queda bloqueante en la microfase/capítulo que habilite la superficie, principalmente C02-05..09 y C04–C07. Las 12 pruebas Chromium **no** son prueba de una consola autenticada.

## 4. Decisión, reversión y puerta siguiente

**C02-04 CERRADA TÉCNICAMENTE: 4/9**, condicionado a que el presente PR documental pase CI último HEAD, integración a `dev` y CI postmerge con comentario final. Se declara cerrado **el diseño y las pruebas del límite público**, no el acceso privado.

- **Siguiente: C02-05**, estados universales de interacción — solo **habilitable después de integrar esta acta**; no empezado. C02 sigue **NO CERTIFICADO** y C03 bloqueado.
- No hay P0/P1 detectado **dentro de este contrato estático tras las pruebas ejecutadas**; no se afirma ausencia de riesgos en backend inexistente ni un producto seguro/certificado.
- **Rollback:** revert de PR #36 y del PR documental de esta acta en `dev`, sin migraciones o datos afectados. No se alteraron `main`, SOLQARYN, DNS, VPS ni producción.
