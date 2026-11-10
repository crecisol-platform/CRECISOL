# CRECISOL — C03-06 | Acta de cierre técnico del módulo Branding / DI

**ESTADO: C03-06 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (6/10); NO CERTIFICADA hasta CI documental propia PREMERGE/POSTMERGE y comprobación exacta.** C03-01..05 CERTIFICADAS (5/10); C03-07 NO INICIADA; **C03 NO CERTIFICADO; C04 BLOQUEADO**, 2/37 capítulos certificados (C01 y C02). El plan vigente de 5/10 es **el estado certificado previo**, no la acreditación adelantada de C03-06.

## 1. Autorización, orden y autoridad original

El propietario solicitó, **antes de ejecutar las pruebas**, el 9 de noviembre de 2026: «Ok, listo, adelante continúa con la siguiente fase y con full cuidado por favor. Sé full estricto, no omitas nada, ¿vale? Trabaja como lo has venido haciendo, revisa absolutamente todo, ¿vale? continúa.» Esta autorización es de **ejecución de C03-06 con requisitos completos**, y **no** se inventa aprobación humana posterior a las CI. No autoriza C03-07, capítulo 04, despliegues, cobros ni escrituras a SOLQARYN.

Documento Maestro Único Word en `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, blob Git `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; PDF lectura blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`. Fuente C03 P01–P07, T01-R03 Application, T01-R04 Infrastructure, T01-R05 API; siete textos, tabla original de nueve filas; ocho puertas MAE-C03-01..08 distintas de diez microfases operativas. Previa C03-05 CERTIFICADA (5/10) por [PR #58](https://github.com/solqaryn-platform/CRECISOL/pull/58) y **11/11 CI POSTMERGE SUCCESS** sobre `12fb19b0c35038c297939e17b27770c4fd63f77e`. Remediación Codex [PR #59](https://github.com/solqaryn-platform/CRECISOL/pull/59), **19/19 CI POSTMERGE SUCCESS** en `d8f1247c537fbb313be2026eeb8ca321e10be714`.

## 2. Entrega técnica e impacto exacto

[PR técnico #60](https://github.com/solqaryn-platform/CRECISOL/pull/60), base `dev` `d8f1247c537fbb313be2026eeb8ca321e10be714`, HEAD PREMERGE `38a59262252bb4605e6f19eae490662da5f426a5`; squash **exclusivamente a dev** `7b96a2111f9f8483bca676b6019e3d07ec679989`. Registro vertical real `PublicBrandingModule` en `backend/src/Crecisol.Api/Modules/Branding/`: solamente `IPublishedPlatformBrandReader` singleton + `ReadPublishedPlatformBrand` scoped, inyección explícita y rechazo de duplicados/overrides. `Program.cs` obtiene el DSN desde el entorno servidor e invoca exclusivamente `AddPublicBrandingModule(serverDsn)`. Solo GET público `/api/v1/public/branding/platform`; DTO JSON de diez campos intacto, Host allowlist, denegación de Forwarded/X-Tenant-Id/Auth, no-store/nosniff, errores 200/400/404/503 sin información sensible.

**No se registran** puertos privados `IPublishedPlatformBrandRevisionReader`, `MySqlPublishedPlatformBrandRevisionReader`, casos de publicación, Service Locator, resolutores `IServiceProvider`, assembly scanning, endpoints administrativos, otros módulos verticales ficticios, workers o nuevas tablas. Ni `frontend/`, proyectos Domain/Application/Infrastructure de producto, DDL MySQL ni Word/PDF sufrieron modificaciones. No auth privada BOLA/IDOR/MFA, TenantContext shared/dedicated, dashboard, catálogo, pagos, VPS, `main` ni SOLQARYN.

## 3. Evidencia PREMERGE real PR #60

**5/5 CI PREMERGE SUCCESS**, cada ejecución `pull_request/completed/success` sobre SHA `38a59262252bb4605e6f19eae490662da5f426a5`:

- [C03-06 A/B/C, DI + MySQL #42752596034](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596034): .NET 10 Release 0 advertencias/errores, **25/25 assertions** de lifetimes, scope, duplicate/override, fail-closed; **15/15 mutaciones adversariales**; 51+19+35+37+12+57 regresiones .NET y MySQL8.4 SELECT-only; **27/27 HTTP** público extra.
- [C03-05 regresión API/DTO #42752596091](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596091): 57 contratos y 12 mutaciones previas; regresión pública completa.
- [C03-01 integral #42752596048](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596048): Angular22/Chromium, WCAG automatizado, .NET10 y MySQL8.4 desechable, 83 Chromium y 37 casos HTTP históricos.
- [Windows 2022 + Ubuntu 24.04 #42752596100](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596100): preflight local A/B, ruta portable y gates históricos.
- [Foundation #42752596222](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42752596222).
- C01-04 #42752596185 **skipped**, correctamente NO contado como PASS.

## 4. Evidencia técnica POSTMERGE por corroborar antes del acta definitiva

Las ejecuciones `push` creadas para SHA técnico `7b96a2111f9f8483bca676b6019e3d07ec679989` son: C03-06 #42753303252, C03-05 #42753303282, C03-01 integral #42753303327, compatibilidad Windows/Ubuntu #42753303287 y Foundation #42753303579. Estos identificadores **NO son afirmaciones anticipadas de SUCCESS**; exigir que **5/5** terminen `event=push,status=completed,conclusion=success,head_sha=7b96a2111f9f8483bca676b6019e3d07ec679989` antes de certificar o fusionar el acta. No contar `queued`, `in_progress`, `skipped` ni conclusiones fallidas como PASS.

## 5. Triple auditoría, riesgos y reversión

- **A — trazabilidad:** fuente Word SHA, siete párrafos y nueve filas, T01-R03/R04/R05, límite modular y cadena previa C01/C02/C03-01..05 certificada.
- **B — seguridad:** 15/15 mutaciones de DI rechazadas (registro doble, lifetime incorrecto, Service Locator, reflection, puertos privados, cliente/host y rutas privadas); 25 assertions reales de alcance y DSN no configurado; errores genéricos.
- **C — arquitectura/regresión:** cuatro referencias C# permitidas, compilación .NET 10 Release real, Angular 22 y Chromium, API/MySQL8.4 HTTP SELECT-only con red aislada; cero cambios a endpoints o dependencias de Domain.
- **Riesgos P0/P1:** exposición de puerto de revisión interno, código malicioso desde configuración, DSN elegido por request, duplicado de DI, fuga multitenant, registro de escritura. Ninguno está habilitado por los cambios revisados. No asumir cobertura de auth privada no implementada.
- **Rollback:** revert por PR del squash técnico `7b96a2111f9f8483bca676b6019e3d07ec679989` y del acta posterior solo a `dev`, sin migraciones, cambios productivos ni secretos que revertir.

## 6. Gate separado de formalización

El control `qa/c03_06_closure_gate.py` debe ejecutar 12/12 mutaciones adversariales documentales y validar evidencia, autorización previa, 5/5 técnica PREMERGE, 5/5 técnica POSTMERGE y cierre histórico; solo se acepta tras real .NET/MySQL/Angular y auditorías A/B/C. La CI del PR de esta acta debe completarse **PREMERGE y POSTMERGE** sobre sus SHA exactos. **Antes de ello C03-06 NO CERTIFICADA**; C03-07 NO INICIADA, C03 NO CERTIFICADO y C04 BLOQUEADO. El estado canónico 6/10 solo se promoverá con la constancia POSTMERGE realmente verificable, sin sobrescribir este antecedente.
