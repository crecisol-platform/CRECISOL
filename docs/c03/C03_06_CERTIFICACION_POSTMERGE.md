# CRECISOL — C03-06 | Constancia definitiva posterior al acta

**ESTADO VERIFICADO: C03-06 CERTIFICADA (6/10)** en el alcance exclusivo del módulo vertical público Branding e inyección explícita de dependencias. **C03-05 CERTIFICADA (5/10)** previamente; **C03-07 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02)**. Esta certificación de microfase **no certifica** el capítulo 03 completo, ni multitenancy privado, autenticación o producción.

## Autoridad, precedencia y consentimiento

- Repositorio autorizado exclusivamente `solqaryn-platform/CRECISOL`, integración mediante [PR #60](https://github.com/solqaryn-platform/CRECISOL/pull/60) y [PR #61](https://github.com/solqaryn-platform/CRECISOL/pull/61), solo `dev`; sin escritura en `main`, `solqaryn/Solqaryn`, VPS, DNS, cobros o producción.
- Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`: blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. PDF de lectura blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`. Ambos archivos permanecieron idénticos al origen aprobado.
- **Autorización específica del propietario ANTES de las pruebas** del 9 de noviembre de 2026: «Ok, listo, adelante continúa con la siguiente fase y con full cuidado por favor. Sé full estricto, no omitas nada, ¿vale? Trabaja como lo has venido haciendo, revisa absolutamente todo, ¿vale? continúa.» La frase «Sé full estricto, no omitas nada» es parte de esa instrucción, **no** una aprobación inventada después de CI.
- C03-05 quedó CERTIFICADA por [PR #58](https://github.com/solqaryn-platform/CRECISOL/pull/58) y 11/11 POSTMERGE; remediaciones Codex [PR #59](https://github.com/solqaryn-platform/CRECISOL/pull/59) con 19/19 POSTMERGE. La [acta técnica de C03-06](C03_06_ACTA_MICROFASE.md) es la instantánea anterior a verificar CI documental y no debe reescribirse retroactivamente. [Cierre real del acta PR61](https://github.com/solqaryn-platform/CRECISOL/pull/61#issuecomment-6758889847).

## Evidencia CI verificable — cuatro conjuntos independientes

### PR técnico #60 — PREMERGE
**5/5 CI PREMERGE SUCCESS**. Todos los runs se verificaron como `event=pull_request,status=completed,conclusion=success,head_sha=38a59262252bb4605e6f19eae490662da5f426a5` mediante API de GitHub.

| Suite | Ejecución real | Estado |
| --- | --- | --- |
| DI A/B/C | [42752596034](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596034) | SUCCESS |
| C03-05 DTO | [42752596091](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596091) | SUCCESS |
| C03-01 integral | [42752596048](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596048) | SUCCESS |
| Windows+Ubuntu | [42752596100](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596100) | SUCCESS |
| Foundation | [42752596222](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596222) | SUCCESS |

### PR técnico #60 — POSTMERGE
**5/5 CI POSTMERGE SUCCESS**. Todos los runs se verificaron como `event=push,status=completed,conclusion=success,head_sha=7b96a2111f9f8483bca676b6019e3d07ec679989` mediante API de GitHub.

| Suite | Ejecución real | Estado |
| --- | --- | --- |
| DI A/B/C | [42753303252](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753303252) | SUCCESS |
| C03-05 DTO | [42753303282](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753303282) | SUCCESS |
| C03-01 integral | [42753303327](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753303327) | SUCCESS |
| Windows+Ubuntu | [42753303287](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753303287) | SUCCESS |
| Foundation | [42753303579](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753303579) | SUCCESS |

### PR documental #61 — PREMERGE
**5/5 CI PREMERGE SUCCESS**. Todos los runs se verificaron como `event=pull_request,status=completed,conclusion=success,head_sha=2db80d42fa30dea79abd48f4cd455b80e0c25788` mediante API de GitHub.

| Suite | Ejecución real | Estado |
| --- | --- | --- |
| DI + acta | [42753672733](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753672733) | SUCCESS |
| C03-05 DTO | [42753672777](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753672777) | SUCCESS |
| C03-01 integral | [42753672741](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753672741) | SUCCESS |
| Windows+Ubuntu | [42753672762](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753672762) | SUCCESS |
| Foundation | [42753672757](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42753672757) | SUCCESS |

### PR documental #61 — POSTMERGE
**5/5 CI POSTMERGE SUCCESS**. Todos los runs se verificaron como `event=push,status=completed,conclusion=success,head_sha=a1923d117febc5fd4e5a544b558c0f85ad517b44` mediante API de GitHub.

| Suite | Ejecución real | Estado |
| --- | --- | --- |
| DI + acta | [42754431737](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42754431737) | SUCCESS |
| C03-05 DTO | [42754431655](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42754431655) | SUCCESS |
| C03-01 integral | [42754431786](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42754431786) | SUCCESS |
| Windows+Ubuntu | [42754431545](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42754431545) | SUCCESS |
| Foundation | [42754431582](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42754431582) | SUCCESS |


Los cuatro conjuntos anteriores prueban **5/5 CI PREMERGE y 5/5 CI POSTMERGE tanto del PR técnico #60 como del documental #61**. El workflow C01-04 #42752596185 estaba `skipped` en la PR técnica, y **no** se cuenta como SUCCESS. La certificación documental posterior sobre `dev` es el commit exacto `a1923d117febc5fd4e5a544b558c0f85ad517b44`. Se conservan los comentarios de evidencia en [PR #60](https://github.com/solqaryn-platform/CRECISOL/pull/60#issuecomment-6758711785) y [PR #61](https://github.com/solqaryn-platform/CRECISOL/pull/61#issuecomment-6758889847).

## Triple auditoría y pruebas

**Auditoría A — fuentes y trazabilidad.** Word SHA original y mapa de C03: siete unidades y nueve filas, T01-R03/R04/R05, límites Domain/Application/Infrastructure/API y precedencia C03-01..05. Sin modificar autoridad Word/PDF.

**Auditoría B — seguridad.** **15/15** mutaciones adversariales de composición DI rechazadas (Service Locator, reflexión, registros duplicados y overrides, lifetimes, puertos/revisión privados, DSN desde cliente, rutas no públicas), **25/25** contratos .NET de DI real en scopes, **12/12** alteraciones maliciosas del acta rechazadas. El nuevo gate de estado verifica además 16 falsificaciones documentales antes de poder avanzar a C03-07. Los resultados no se atribuyen a autenticación privada no implementada.

**Auditoría C — arquitectura/regresión real.** .NET 10 Release con cero errores y advertencias; regresiones Domain, Application, Infrastructure y DTO, 57/57 contratos JSON anteriores, MySQL8.4 LTS 8.4.13 desechable SELECT-only y sin red de producción, **27/27** peticiones HTTP nuevas (incluyendo denegaciones y fallos 404/503), Angular 22 con **83 Chromium** y **37 casos HTTP** históricos. Regresiones Windows 2022/Ubuntu 24.04 A/B sin inventar marcadores de CI ni falsificar resultados del runtime C.

## Alcance realmente certificado

Se añade exclusivamente `PublicBrandingModule` con dos servicios explícitos: `IPublishedPlatformBrandReader` singleton del lector público auditado y `ReadPublishedPlatformBrand` scoped. `Program.cs` delega el registro, mantiene único `GET /api/v1/public/branding/platform`, recibe el DSN solo del entorno servidor, conserva el DTO camelCase de marca y evita registrar la revisión interna. **No se certifican BOLA/IDOR/MFA** privados, tenant routing shared/dedicated, catálogos, panel administrativo, medios, workers, outbox, pagos, escrituras ni despliegues reales. Sin cambios en `frontend/`, `database/` productiva, Domain/Application/Infrastructure de producto, `main` o SOLQARYN.

**Límite secuencial:** solo **C03-06 CERTIFICADA (6/10)**; **C03-07 NO INICIADA**, no se inicia automáticamente por esta constancia; **C03 NO CERTIFICADO**, **C04 BLOQUEADO**, capítulos completos **2/37** (C01 y C02).

**Rollback:** revert mediante PR independiente sobre `dev` de los commits squash técnicos `7b96a2111f9f8483bca676b6019e3d07ec679989`, documental `a1923d117febc5fd4e5a544b558c0f85ad517b44` y de la actualización de estado que formalice esta constancia. No existen migraciones productivas, ni credenciales ni recursos externos que revertir.
