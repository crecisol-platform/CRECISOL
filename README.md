# CRECISOL

**Tu negocio, a tu manera.** — SaaS multitenant en desarrollo para comercio, servicios y membresías.

## Estado vigente (GitHub, noviembre de 2026)

**C01 CERTIFICADO (8/8), C02 CERTIFICADO (9/9; solo público/sintético); C03-05 CERTIFICADA (5/10) y C03-06 CERTIFICADA (6/10).**
**2/37 capítulos certificados (C01 y C02). C03-07 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO.**

C03-06 quedó certificada exclusivamente para el módulo público Branding/DI después del [PR #61](https://github.com/solqaryn-platform/CRECISOL/pull/61), **5/5 CI PREMERGE y 5/5 CI POSTMERGE SUCCESS** sobre `a1923d117febc5fd4e5a544b558c0f85ad517b44`. [Constancia definitiva C03-06](docs/c03/C03_06_CERTIFICACION_POSTMERGE.md). El cierre anterior de C03-05 se verificó en el [PR documental #58](https://github.com/solqaryn-platform/CRECISOL/pull/58) y sus **11/11 CI POSTMERGE SUCCESS**, sobre el commit 12fb19b0c35038c297939e17b27770c4fd63f77e. [Acta y evidencia definitiva C03-05](docs/c03/C03_05_CERTIFICACION_POSTMERGE.md).

**Autoridad funcional:** Documento Maestro Único Word y PDF de lectura, huellas en [MASTER_REFERENCE.md](docs/MASTER_REFERENCE.md). **Autoridad del estado vigente:** [PLAN_37_CAPITULOS.md](docs/PLAN_37_CAPITULOS.md). Las actas y notas antiguas conservan sus estados históricos, pero no sustituyen el encabezado de estado actual.

## Qué existe y qué falta

- frontend/: Angular 22, sitio de marca pública, experiencias, demostraciones, alta de empresas, harness sintético y pruebas Chromium; Node.js 24 para tooling.
- backend/: .NET 10, Domain/Application/Infrastructure/API, contratos tipados, validación HTTP y solo una ruta GET pública de marca PLATFORM.
- database/: esquema MySQL 8.4 de marca inicial y pruebas desechables, sin datos ni conexiones productivas.
- qa/: pruebas reales de integración/CI, auditorías A/B/C y evidencias posteriores por SHA.
- .agents/skills/crecisol-project-governance/: única Skill local propia de CRECISOL.

No se ha implementado aún autenticación/MFA/RBAC privados, multitenancy shared/dedicated operativo, panel administrativo, CRUD de negocios, catálogo/inventario, pagos, workers, outbox ni despliegues. No es un SaaS productivo.

## Comprobación local en Windows, Linux y macOS

Desde la raíz del repositorio con Python 3.11+:

```powershell
py -3.11 qa\c03_local_preflight.py
py -3.11 qa\test_c03_portability.py
```

En Linux/macOS: `python3 qa/c03_local_preflight.py` y `python3 qa/test_c03_portability.py`.

El preflight ejecuta **solo auditorías A/B de fuente y seguridad estáticas de C03-01..06, además de 16/16 negativos documentales del estado 6/10** y comprobación de rutas Windows. **No ejecuta auditoría C, compilaciones .NET/Angular, MySQL, Chromium ni certificación.** Las auditorías C exigen pruebas reales y marcadores temporales .c03-*-ci-proof generados después de los pasos correspondientes en GitHub Actions; no se crean, falsifican ni versionan para hacer pasar pruebas locales. [Workflow de compatibilidad](.github/workflows/c03-local-portability.yml).

## Reglas de trabajo

Leer [AGENTS.md](AGENTS.md) y la Skill local. Usar ramas feature/* y PR hacia dev, sin alterar main. Revisión de diff, seguridad multitenant y autorización backend antes de cualquier avance. No tocar SOLQARYN ni VPS, DNS, producción, pagos ni secretos reales. Las protecciones automáticas de ramas no están activas; se compensan con disciplina manual y CI.

## Historia fundacional — no vigente

El **8 de noviembre de 2026** el repositorio comenzó con **0/37 capítulos certificados**. Es un dato del origen del proyecto, **no el estado presente**. [Referencias visuales históricas](docs/referencias-visuales/README.md) son inspiración no normativa; se respeta el logo azul oficial.
