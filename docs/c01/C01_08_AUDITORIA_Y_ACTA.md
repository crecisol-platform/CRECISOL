# CRECISOL — C01-08 · Auditoría integral de capítulo 01 y acta de conformidad

**ESTADO: AUDITORÍA EN PREPARACIÓN, PENDIENTE DE CI FINAL/POSTMERGE Y ACEPTACIÓN EXPRESA DEL PROPIETARIO.** NO equivale a certificación. 0/37 capítulos certificados; C02 no habilitado. Las microfases C01-01 a C01-07 están cerradas técnicamente (7/8). No se presumen pruebas de este PR antes de consultar sus GitHub Actions.

## A. Alcance, autoridad y fuentes

Repositorio exclusivo: solqaryn-platform/CRECISOL. GitHub dev/feature, no main ni SOLQARYN. Word fuente autoritativa docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e y blob 214bf9c3d9763be23a77b7e0c464359584edde46 según MASTER_REFERENCE.md. Matriz C01-01: 34 párrafos, 21 filas y anexos, con equivalencia MAE-C01-01 a MAE-C01-08. Fuente y estructura se verificarán con el script real qa/c01_verify_source.py (no afirmar lectura humana del Word si no ocurrió).

Objetivo C01-08: revisión integral de la identidad pública CRECISOL, plataforma/tenant, arquitectura frontend/backend/database, aceptación de marca, seguridad de API y datos sintéticos, experiencia responsive, accesibilidad, rendimiento de bundle, regresión de 7 microfases, problemas abiertos y contrato de parametrización. No introducir endpoint admin ni panel privado antes de capítulos habilitados.

## B. Matriz de dependencias y entregables

| Microfase | Evidencia resumida | Estado |
| --- | --- | --- |
| C01-01 | PR #11, matriz 55 identificadores y A/B/C de fuente | CERRADA |
| C01-02 | PR #13, .NET 10 contratos (51 aserciones) | CERRADA |
| C01-03 | PR #16, MySQL 8.4 y pruebas positivas/negativas | CERRADA |
| C01-04 | PR #18, API pública solo lectura y 37 HTTP | CERRADA |
| C01-05 | PR #20/#21, Angular 22 responsive, SEO, WCAG | CERRADA |
| C01-06 | PR #24/#25, PNG aprobado, header/hero, 28 Chromium | CERRADA |
| C01-07 | PR #26/#27, MySQL/API/Angular reales y 27 Chromium | CERRADA |
| C01-08 | Triple auditoría INTEGRAL, CI último HEAD/postmerge, acta y aceptación explícita | EN AUDITORÍA |

## C. Programa de pruebas integral con ejecución real

La CI C01-08 deberá combinar en el mismo runner de GitHub y para el último HEAD:
- Verificación byte-exacta del Word y trazabilidad de la matriz, sin cambiar maestro.
- Node 24.15, npm ci desde lock sin scripts de instalación, npm audit de dependencias de producción y build Angular 22 con presupuestos.
- Logo original verificado por SHA, variante isotipo y preview segura, recuperación si falla.
- Compilación ASP.NET Core 10 Release; 51 aserciones Domain, 19 Application.
- MySQL 8.4 desechable, API y Angular reales, 37 HTTP positivos/negativos, 7 Chromium integrales, sin mocks en estos casos.
- Regresión UI Chromium de C01-05/06 y nuevos casos C01-08 de versión de marca: header, footer, favicon, hero y SEO con nueva publicación; 404, URL maliciosa, ausencia de favicon, no marca antigua.
- Auditoría A (trazabilidad/función), B (seguridad/aislamiento) y C (arquitectura/rendimiento/UX/accesibilidad). Axe automatizado WCAG 2.2 AA NO implica lector de pantalla humano.

**Resultado actual de la CI C01-08: NO EJECUTADO en el momento inicial de esta acta.** Registrar pruebas como PASS solo si existe evidencia real de último SHA y posmerge, de lo contrario NO GO.

## D. Controles negativos, riesgos y límites

- Rechazo de host falso, cabeceras Forwarded, tenant-id, usuario falso, URLs arbitrarias, XSS/HTML/script, escrituras HTTP sobre API pública, CORS no autorizado, API sin configuración, publicaciones inexistentes y contenido inválido.
- Aislamiento entre plataforma y tenant en MySQL con SELECT-only, sin cachés/secretos/datos reales. No equivale a autorización de funciones privadas que aún NO existen.
- Ningún despliegue VPS, DNS, Hostinger productivo, Cloudflare R2 real, cobros, activación de servicios o escritura en solqaryn/Solqaryn.
- El contrato transversal de editabilidad se registra en C01_08_PARAMETRIZACION_TRANSVERSAL.md. La UI administrativa de carga/edición, publicación y push a sesiones abiertas es futura y no debe darse por implementada; las cadenas editoriales actuales de plantilla siguen parcialmente fijas. Los controles de backend nunca son modificables arbitrariamente por admins.
- Advertencias Node DEP0040/DEP0169 y actions Node20 a Node24 permanecen visibles como seguimiento de dependencias. No ocultar fallos previos ni degradar pruebas para obtener PASS.
- Rollback: revert del PR C01-08, conservando el snapshot anterior; no modificar main ni las actas cerradas.

## E. Gates definitivos (NO CERTIFICAR anticipadamente)

1. Evidencia de tres auditorías INTEGRALES independientes A/B/C en CI real sin P0/P1 C01 abiertos; requisitos fuente/contratos/regresión comprobados.
2. PR técnico revisado con CI del último HEAD SUCCESS; cambios solo CRECISOL; git SHA conservado, sin force.
3. Merge a dev con expected_head_sha y CI postmerge sobre el SHA integrado en SUCCESS.
4. Acta final integrada y evidencia de resultado documental/cierre; microfase C01-08 auditada técnicamente.
5. **Aceptación humana expresa del propietario tras leer los resultados y limitaciones.** Sin ella: C01 NO CERTIFICADO, 0/37 y C02 no habilitado. No interpretar «continúa» como aceptación final del capítulo.

**Conformidad del propietario: PENDIENTE. Certificación del capítulo: NO. Capítulo C02: BLOQUEADO.**
