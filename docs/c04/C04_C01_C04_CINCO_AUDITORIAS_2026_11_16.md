# CRECISOL — Cinco auditorías independientes integrales C01–C04

**Fecha:** 16-11-2026. **Repositorio autorizado:** `crecisol-platform/CRECISOL`. **Rama base de `dev`:** `43ec01c7b5608a10af2ed1ec59783483018ac8f9`. **Rama de auditoría:** `feature/c04-five-independent-audits-20261116`.

## Estado inicial y decisión de gobernanza

El propietario solicita cinco auditorías completas y retrospectivas de los cuatro capítulos ya certificados históricamente: **C01 8/8; C02 9/9; C03 10/10; C04 12/12**. Se realiza como una revisión **transversal de la puerta correctiva C04**, sin empezar trabajos de C05 ni cambiar comportamiento aprobado de C01–C04. No se alteran `main`, datos productivos, el Documento Maestro Único DOCX/PDF, VPS, DNS, cobros, SOLQARYN ni proveedores externos.

**ESTADO DE ESTA NUEVA REVISIÓN AL CREAR LA RAMA: cinco auditorías pendientes de ejecutar/validar por SHA. NO CERTIFICADO EN ESTE CORTE.** Las certificaciones históricas se conservan y no se convierten en garantía de que todo sea perfecto. Las pruebas automatizadas no sustituyen una inspección humana real de todas las vistas, una prueba de carga de producción o un pentest independiente.

## Cinco auditorías verdaderamente separadas

| Auditoría | Riesgo/control y prueba real en CI | Criterio de aprobación |
| --- | --- | --- |
| **1/5 · Fuente, función y trazabilidad** | SHA Word/PDF y referencia maestra; actas de C01–C04; 37 capítulos y estado prospectivo; guardas fundacionales y normativas C01, C02, C03 y C04 | Fuente íntegra; C01–C04 trazables; no certificar C06; negativa ante inconsistencias |
| **2/5 · Backend y autorización** | Límites de Host, forwarded headers, server DSN, registro local condicionado; C03 clean graph; C04-12 B y ofensiva C04-11 B; API ASP.NET10 Release + 20 suites ejecutables .NET positivas/negativas | Sin fallos en pruebas de Domain/Application/API, roles, tenant, sucursal/recurso, falsificación y denegación |
| **3/5 · Base de datos multitenant** | **9 fixtures reales MySQL8.4** de C04: FK/owner/shared, adaptador .NET, provisión, lifecycle, migración y rollback, fencing/concurrencia, cache/job/media/export y ofensiva; C03 outbox + C01 HTTP real | Privilegios y aislamiento A/B, rollback/restore, concurrencia, SQL negativos sin acceso a bases reales |
| **4/5 · Frontend y calidad visual** | npm lock y auditoría de dependencias de producción; Angular22 types/build/presupuestos/assets; **83 pruebas Chromium C01/C02**; ocho capturas nuevas desktop/móvil (inicio + tres páginas), axe WCAG 2.2 AA automático, overflow y headings | Todas las pruebas y capturas útiles producidas, sin regresiones visibles automatizables. **La aprobación estética humana sigue separada** |
| **5/5 · Integración, configuración y CI** | C03 preflight, portabilidad y arquitectura; workflow permissions de C01/C02/C03/C04 y guardas antifuga; Node24/.NET10; **C01 API+MySQL8.4+Angular22+Chromium en stack real** sin datos/hosts externos | Sin bypass de credenciales CI, rutas inseguras, errores HTTP/stack o pruebas omitidas |

**Evidencia técnica:** `.github/workflows/c04-five-independent-audits.yml`, cinco jobs independientes, uno de consolidación que falla si alguno termina `failure`, `cancelled` o `skipped`; ocho capturas sintéticas emitidas en artefacto de vida corta. Evidencia PREMERGE y POSTMERGE debe registrarse **por SHA y run IDs reales**. No simular ejecuciones ni certificar por marcadores escritos sin comandos previos.

## Inspección de arquitectura y seguridad sin alterar fuentes de producción

- **Frontend:** solo se añade `frontend/tests/c04-five-audits-public-visual.spec.ts`; todo resultado es un fixture público de C01/C02, no una interfaz real de empresas/operadores, y jamás habilita rutas privadas.
- **Backend:** cero cambios funcionales. Se compila y ejecutan contratos positivos/adversariales en C01–C04 y se conserva el control de DSN desde el servidor. Las APIs locales de C05 no se activan por este corte.
- **Database:** cero cambios de datos/DDL/migraciones. Cada ensayo MySQL usa contenedor descartable y cuentas sintéticas. El modelo C04 compartido/dedicado no se convierte por esta revisión en un router productivo.
- **GitHub/CI:** workflow nuevo de `contents: read`, checkout sin credenciales persistidas, acciones ancladas a SHA, sin privilegios de despliegue; agregación a cinco puertas.

## Hallazgos y límites anteriores que NO se deben esconder

- **GOV-P2 · Protección GitHub:** las respuestas API consultadas el 16-11-2026 muestran `dev.protected=false` y `main.protected=false`. El proceso por PR y CI mitiga, pero **no sustituye la protección técnica de ramas**. Verificar viabilidad de rulesets sin cambiar el plan/visibilidad del repositorio sin autorización expresa.
- **DOC-P2 · Estado histórico fácilmente confundible:** `AGENTS.md`, la Skill de repositorio, el plan y varias actas conservan instantáneas antiguas (p. ej. C03 pendiente), mientras `docs/ESTADO_VIGENTE_CAPITULOS.md` deja C01–C05 certificados en sus alcances. No borrar historial; una futura normalización prospectiva única deberá evitar encabezados obsoletos y mantener invariantes de auditoría.
- **UX-P2 · Revisión visual humana:** 83+8 pruebas Chromium, axe y screenshots son evidencia automática, **no certificación humana de estética, contraste manual, lector de pantalla ni satisfacción de usuarios**. Las pantallas de negocio de C04 aún no existen, y no procede inventarlas.
- **SEG-LIMITACIÓN:** el propio C04 histórico certifica **contratos, arquitectura y fixtures SQL sintéticos**, no un SaaS multitenant desplegado ni todas las operaciones privadas/productivas.
- **C05-08 issue #160**: intermitencia del test de replay TOTP de C05 detectada en el PR #159, corregible al abrir C05. No pertenece a la implementación de C01–C04 ni se considera resuelta solo porque el segundo intento haya sido SUCCESS.

## Protocolo de fallo, cierre y reversión

**NO-GO** si falla alguna de las cinco auditorías, si un contenedor accede a producción, si una comprobación crítica se omite, si hay fuga entre tenants, bypass de autoridad, alteración de hashes, dependencia de SOLQARYN o regresión real. Corregir defectos pertinentes en una rama del capítulo correspondiente con su propio análisis de impacto; no ocultar fallos históricos ni certificar sin POSTMERGE.

El acta final se debe añadir **solo después** de observar 5/5 nuevas auditorías `SUCCESS` PREMERGE, árboles Git y SHAs verificados, integración controlada en `dev` y 5/5 POSTMERGE `SUCCESS`. Se debe adjuntar un dictamen independiente con defectos encontrados, riesgos residuales, pruebas no realizadas y juicio de UX limitado. Revertir exclusivamente los archivos de auditoría si fracasa un gate; no existe reversión SQL.
