# C01-05 · Acta técnica de Angular 22, SEO, responsive y accesibilidad

**Fecha:** 2026-11-08 en Honduras (evidencias GitHub Actions 2026-11-09 UTC).  
**Estado:** **C01-05 CERRADA TÉCNICAMENTE EN DEV (5/8 microfases)** tras auditorías A/B/C, Angular22/Chromium/WCAG y merge con CI post-merge confirmado. **Capítulo C01 NO CERTIFICADO (0/37)**.  
**Avance permitido:** C01-01/02/03/04 ya cerradas; C01-05 en validación; C01 NO CERTIFICADO, 0/37 capítulos.  
**Origen:** único Documento Maestro CRECISOL SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e, matriz C01-01, C01_PREPARACION y API C01-04.

## Evidencia real observada

- [CI C01-05 #42558658894](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658894): **SUCCESS, 3/3 trabajos** en HEAD 3e296175b8e23837c53c1e9035d196edfde0a411.
- [Foundation del mismo HEAD #42558658881](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658881): **SUCCESS**.
- Angular **22.2.4** con Node **24.15.0**, TypeScript **6.0.3**, npm ci desde lock SHA Git y build de producción: **PASS**, sin sobrepasar presupuestos. Initial chunks: **233.23 kB** raw / **66.68 kB** transferencia estimada; home lazy **23.79 kB** raw y not-found lazy **1.29 kB** raw.
- **npm audit --omit=dev --audit-level=high:** **0 vulnerabilidades reportadas** entre dependencias de producción en ese momento. No significa que cada dependencia dev o futura versión esté libre de riesgos; el lock registra 361 paquetes y npm ci reproduce el grafo.
- **Chromium Playwright: 11/11 casos PASS**. Se verificó browser real, URL same-origin sin tenant, SEO/metadata dinámico, ausencia de contenido privado, móvil **360px** sin overflow, teclado/skip link, WCAG 2.2 AA automatizado por axe en desktop y 375px, XSS/fuente/color/favicon ajenos, 404/503, reintento, falta de presentación, rutas no autorizadas, reduced motion.
- **Auditoría A PASS:** Maestro SHA validado por c01_verify_source.py, matriz + código + condiciones.
- **Auditoría B PASS:** validación de DTO exacta, URLs/activos locales, no HTML/JS ejecutable, sin caché local, sin tenants, fail-closed.
- **Auditoría C PASS:** Angular22 standalone + lazy routes + Signals + Node24/TypeScript6, responsive, performance, presupuesto CSS, SEO conservador, reversión.

## Hallazgos corregidos durante implementación

- [CI inicial #42558340548](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558340548): auditoría B falló por falso positivo textual de la palabra «localStorage» en un comentario; no existía lectura de almacenamiento persistente. Comentario aclarado y auditorías reejecutadas.
- Primer Chromium real detectó **contraste 3.64:1** en una nota pequeña sobre superficie clara frente al mínimo requerido 4.5:1 de WCAG AA. Se ajustó color de texto a un tono más oscuro; se mantuvo la prueba y **11/11** pasaron, sin desactivar reglas axe ni excepciones.
- Se capturó package-lock de la ejecución real de Node24 en GitHub, se verificó que incluye router Angular22.2.4, Playwright y axe, y se incorporó por blob Git. La CI final usa npm ci (no npm install flotante).

## Alcance exacto y riesgos NO cerrados

**Frontend:** se implementaron solo una interfaz de identidad pública, error/carga segura, navegación y metadatos de marca publicados. El usuario no puede elegir tenant, ID o cabecera que conceda acceso. El API C01-04 no se cambia; frontend recibe contrato fijo por mismo origen. Las pruebas de navegador interceptan únicamente el GET mediante fixture sintético; **NO prueban una ejecución Angular real contra MySQL/API reales como sistema integral**, que queda para C01-07 y puertas de capítulos posteriores.

**C01-06 obligatorio:** el logotipo azul oficial del Word no se ha exportado ni instalado en un archivo gráfico aprobado. Solo hay wordmark textual provisional y motivos no identificatorios. La ruta de favicon se enlaza si el API la entrega, pero su archivo y la exactitud de la imagen no se han aprobado. C01-06 verifica el activo antes de considerarlo válido.

**Seguridad/legal/producción:** sin autenticar ni autorizar personas, sucursales o tenants privados; sin SSR, SEO indexable, CSP de proxy, DNS verificado, Hostinger, pagos, servidor de producción, R2, cookies ni dominios externos. HTML preproductivo incluye noindex,nofollow. La accesibilidad automatizada y diseño responsive no equivalen a revisión visual humana completa, pruebas con tecnologías asistivas reales o garantía integral de WCAG; tales comprobaciones se mantienen en C01-07/08.

**Advertencia no bloqueante registrada:** acciones de checkout emiten aviso de runtime Node 20 obsoleto (runner las ejecuta forzado con Node 24); requiere actualización del pin en una revisión de herramientas, sin inferir que afecta al JavaScript compilado. El runtime de Angular sí se verificó con Node 24.15.0.

## Revisión final y reversión

**Cierre acreditado:** [CI final PR #20 #42558828236](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558828236) SUCCESS 3/3 sobre HEAD `a605deba5015425638afa25fb48101c889a9d4bf` (incluye esta acta), [foundation #42558828156](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558828156) SUCCESS; [PR #20](https://github.com/solqaryn-platform/CRECISOL/pull/20) fusionado solo a `dev`, SHA `b902193b954d29f22dbdf8da9b22e48680f45dcd`; [CI de fundación post-merge #42558999820](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558999820) SUCCESS sobre ese merge. Diff revisado: 26 archivos exclusivamente de frontend, qa, CI y documentación C01-05, sin código backend/database ni secretos. Delegación de cierres de microfase en docs/C01_PREPARACION.md; no autoriza C02 ni despliegues.

**Reversión:** revertir el PR #20 vía PR independiente. Sin datos reales, infraestructura, fichero gráfico oficial o servicios de producción que restaurar.

**Secuencia:** tras C01-05 CERRADA, iniciar **C01-06** únicamente para el logo aprobado; C01-07 seguridad/regresión integral y C01-08 auditorías/aceptación; 0/37 capítulos certificados hasta evidencia posterior.

## Cierre documental y siguiente dependencia

**Resultado C01-05:** CERRADA TÉCNICAMENTE (Angular 22, interfaz pública, SEO limitado, responsive, automatización WCAG y negativas navegador), tras CI final del código y post-merge dev. Son **5/8 microfases** cerradas. Siguiente permitida **C01-06**: identidad visual del logotipo aprobado, sin redibujar, deformar ni sustituir; requiere el archivo individual auténtico y evidencia de procedencia. C01-07/08 siguen pendientes; **0/37 capítulos certificados**.
