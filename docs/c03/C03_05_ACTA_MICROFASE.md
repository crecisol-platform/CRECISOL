# CRECISOL — C03-05 · Acta de cierre técnico y certificación condicionada

**ESTADO: C03-05 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (5/10), FORMALIZACIÓN EN CURSO; TODAVÍA NO CERTIFICADA hasta las CI propias del acta PREMERGE y POSTMERGE.** C03 NO CERTIFICADO; C04 BLOQUEADO; C03-06 NO INICIADA; 2/37 capítulos certificados (C01/C02).

## Autorización humana y orden

El propietario solicitó expresamente, ANTES de las pruebas: «OK, adelante con ello pues. termina la fase, la microfase 5 ya, ahora por favor.» (9 de noviembre de 2026). Esta autorización expresa completa C03-05 condicionada a pruebas y acta, NO es una respuesta posterior a resultados ya obtenidos, y NO autoriza C03-06, C04, VPS, producción, pagos o SOLQARYN. La microfase anterior C03-04 CERTIFICADA (4/10) en PR #56, squash dev 6cb6cb79792efdddeab5fef0f0596a6532ed3afc, 10/10 workflows POSTMERGE SUCCESS.

## Fuente y alcance exacto

Word canónico docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx; blob 214bf9c3d9763be23a77b7e0c464359584edde46; SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e; PDF solo lectura sin cambios. Capítulo 03: siete unidades originales, tabla principal nueve filas, T01-R03/T01-R04, P03/P04 y DIF-C03-01. Ocho puertas normativas MAE-C03-01..08 NO equivalen a diez microfases técnicas.

Único GET público /api/v1/public/branding/platform; DTO allowlist de diez propiedades y seis campos de presentation, compatible con parser Angular. Validador GET sin cuerpo, query, Authorization, X-Tenant-Id, Content-Length, Transfer-Encoding, headers de override; Host allowlist y forwarded-host denegados. 400 para petición inválida, 404 sin publicación, 503 sin DSN o con contenido inválido. Sin endpoint privado, cambios de DI ni acceso tenant.

## PR técnico #57 — evidencia verificable

PREMERGE sobre HEAD 3e444169b7beeacc6c891318588a582c893aa516: [C03-05 A/B/C #42742553465](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42742553465), [C03-01 integral #42742553132](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42742553132), [Foundation #42742553083](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42742553083): 3/3 completed/success. C01-04 skipped NO se contabiliza como PASS.

PR #57 fusionado por squash a dev SHA 9d52ed9539b72611cc15bd71433ce7d7707dd13f. Gates POSTMERGE de ese mismo SHA: [API C03-05 #42743329418](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42743329418), [regresión integral C03-01 #42743329202](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42743329202), [Foundation #42743329425](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42743329425). Estos IDs deben verificarse TODOS push/completed/success antes de certificar; registrar la revisión final del estado, no anticiparla.

**Auditoría A:** Word SHA real, mapeo de API y contrato Angular; **Auditoría B:** 12/12 mutaciones adversariales rechazadas (filtración de DTO, rutas privadas, host/headers/query, cuerpo); **Auditoría C:** .NET10 Release cero warnings/errores; 57/57 aserciones DTO/JSON/HTTP, 154 regresiones .NET (51+19+35+37+12), MySQL8.4.13 desechable UNIX socket --network none con SELECT-only, 27 HTTP C03-05 (24 baseline + absent + invalid + unconfigured), 83 Chromium y 37 casos HTTP históricos de C03-01. Evidencia real en los dos workflows PREMERGE y su réplica POSTMERGE.

## Impacto, exclusiones y rollback

Backend: solo API DTO/middleware de validación; tests nuevos .NET; QA, workflow y script SQL de pruebas. No modificar frontend, Domain/Application/Infrastructure productivos, MySQL DDL, API privada, Word/PDF, main ni SOLQARYN; sin despliegue, VPS, DNS, pagos ni datos reales. NO se certifican BOLA/IDOR/MFA privados, TenantContext, tenant router, backups, medios, auth admin, publicación CAS ni outbox. Reversión por revert mediante PR a dev del squash técnico 9d52ed9539b72611cc15bd71433ce7d7707dd13f y acta posterior; sin migraciones productivas.

## Puerta formal de C03-05

La aprobación inicial del propietario queda condicionada a evidencia real. Gate qa/c03_05_closure_gate.py: A/B/C, archivo Word, 3/3 PREMERGE y 3/3 POSTMERGE técnico, 12/12 negativas de documentación, preservación histórica C01/C02/C03-01..04 y límite exacto 5/10. La nueva CI de este PR documental exige PREMERGE y POSTMERGE propios sobre SHA exacto, todos completed/success; skipped NO equivale a éxito.

**Solo tras comprobar ambas puertas:** C03-05 CERTIFICADA 5/10. C03 NO CERTIFICADO, C03-06 NO INICIADA y C04 BLOQUEADO. Este acta NO anticipa un futuro resultado de GitHub Actions.