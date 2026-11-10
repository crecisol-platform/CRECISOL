# Fase 1 · Capítulo 01 — CRECISOL: identidad, visión, alcance y principios

**Estado vigente de C01:** CAPÍTULO 01 CERTIFICADO (8/8). **Antecedente al certificar C01: 1/37 capítulos certificados**; estado global actual **2/37 capítulos certificados (C01 y C02); C03-01 a C03-07 CERTIFICADAS (7/10), C03-05 CERTIFICADA (5/10), C03-06 CERTIFICADA (6/10), C03-07 CERTIFICADA (7/10) mediante PR #63 (8/8 CI POSTMERGE SUCCESS en a256885afa5ee2f639bd4ed6f7d8595b709e6409) y PR #64 (5/5 CI POSTMERGE SUCCESS en b1e019afabfb44f05076024b8d34cde74a340acc); C03-08 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO.** C01 conserva sus pruebas y acta históricas. C03-07 no certifica seguridad privada ni el capítulo C03 completo.

## Contrato de alcance

Construir una sola identidad de producto CRECISOL, basada en el logotipo azul oficial que aparece en la portada del maestro, el lema «Tu negocio, a tu manera.» y el descriptor de comercio, servicios, membresías y crecimiento. La marca de la plataforma se administra de manera independiente de la marca de cada negocio.

Centralizar textos, paleta, identidad institucional, copyright, iconos, enlaces permitidos y metadatos SEO como datos versionados; permitir lectura pública del snapshot aprobado. No dispersar literales de marca en componentes. No habilitar escritura administrativa sensible sin autenticar y autorizar conforme a los capítulos posteriores.

## Orden de microtareas

1. Trazar requisitos de la sección 01 y reglas transversales hasta criterios observables; registrar fuentes y dependencias.
2. Definir contrato tipado de identidad por ámbito plataforma / tenant y estados borrador, vista previa y publicación.
3. Modelar entidades e integridad relacional mínima MySQL (sin exponer datos privados) y evaluar compatibilidad real .NET 10 / proveedor.
4. Construir Domain/Application y endpoint **solo lectura** de marca publicada, con autorización explícita en endpoints privados.
5. Construir Angular 22 ligero con cabecera, pie, SEO, responsive, accesibilidad y snapshot de marca; Node.js 24 solo tooling.
6. Identificar el activo gráfico oficial a partir del archivo aprobado y conservarlo sin deformación ni sustituciones. Para producción de UI se requiere un archivo gráfico independiente aprobado, no solo la vista dentro del Word.
7. Probar coherencia plataforma/tenant, host malicioso, XSS/HTML/JS inyectado, secretos, caché e identidad antigua; documentar pruebas no ejecutadas.
8. Triple auditoría A (trazabilidad), B (seguridad) y C (arquitectura, rendimiento, accesibilidad y UI), acta y aprobación explícita antes de C02.

## Criterios de no ejecución

No usar credenciales ni datos de SOLQARYN, ni desplegar VPS/dominios, ni activar cobros. No comenzar C02 hasta certificación real de C01. La documentación de esta fase **no constituye implementación**.

## Referencias visuales para la identidad de C01

La [biblioteca curada](referencias-visuales/README.md) conserva siete capturas diferenciadas del origen comercial, dos capturas de Atlas/auditoría y un análisis de interacciones de los HTML antiguos. La biblioteca es material histórico **no normativo**: sus estilos verdes, datos de demo y código se descartan.

Durante C01 se evaluarán composición editorial, cabecera/pie, tipografía, jerarquía y vistas previas, siempre con el logo azul oficial y marcas separadas entre plataforma y tenant. Ninguna captura justifica desarrollar prematuramente los flujos de venta, catálogo, pagos o auditoría previstos en otros capítulos. El HTML original no necesita subirse a GitHub para arrancar C01.

## Control secuencial de ocho microfases y delegación de cierre

El usuario autorizó el 8 de noviembre de 2026 que cada microfase se **valide, revise tres veces (auditorías A/B/C de su alcance), documente y fusione a dev sin consulta rutinaria**, únicamente con evidencia real y sin bloqueos P0/P1 atribuibles a su entrega. Ninguna fase posterior podrá empezar sin cierre de la anterior. **La certificación del capítulo completo y el avance a C02 requieren las auditorías integrales, acta y aceptación de C01-08; la delegación de microfases no certifica el capítulo.**

**Numeraciones distintas sin alterar maestro:** las fases técnicas operativas siguen C01-01...C01-08; la secuencia literal del contrato de implementación del Word se referencia como MAE-C01-01...MAE-C01-08 y se enlaza mediante [tabla de equivalencia](c01/C01_01_DEPENDENCIAS_Y_GATE.md).

| Microfase técnica | Entregable y dependencia | Estado |
| --- | --- | --- |
| C01-01 | Matriz de 34 párrafos + 21 filas, anexos, dependencias, tres auditorías del entregable, acta | CERRADA DOCUMENTALMENTE (tres auditorías A/B/C PASS; PR #11 autorizado a dev) |
| C01-02 | Contratos tipados y modelo de ámbito; requiere C01-01 cerrada | **CERRADA TÉCNICAMENTE**: 51/51 aserciones, A/B/C PASS, PR #13 fusionado a dev |
| C01-03 | Diseño MySQL 8.4, migración/configuración futura; requiere C01-02 | **CERRADA TÉCNICAMENTE**: MySQL 8.4 9/9+22/22, 51 contratos net10, A/B/C PASS, PR #16 fusionado a dev |
| C01-04 | Caso de uso y API .NET; requiere C01-03 | **CERRADA TÉCNICAMENTE**: PR #18 integrado; .NET10 51+19, MySQL 8.4.13, 37 HTTP, A/B/C PASS, CI post-merge |
| C01-05 | UI Angular, responsive, accesibilidad y navegador; requiere C01-04 | **CERRADA TÉCNICAMENTE**: PR #20 fusionado, Angular22/Node24, 11 Chromium/WCAG, A/B/C PASS, CI post-merge |
| C01-06 | Logo oficial y presencia visual aprobados; requiere C01-05 | **CERRADA TÉCNICAMENTE**: logo del Word SHA aprobado, cabecera + hero Angular, 28 Chromium, 12 negativos, A/B/C finales PASS, PR #24 y CI post-merge en dev |
| C01-07 | Pruebas positivas, negativas, seguridad y regresión; requiere C01-06 | **CERRADA TÉCNICAMENTE**: PR #26, MySQL 8.4.13 + ASP.NET Core 10 + Angular 22, 37 HTTP, 27 Chromium, 51+19 .NET, A/B/C FINAL PASS y CI post-merge |
| C01-08 | Auditorías integrales A/B/C, acta, conformidad del capítulo; requiere C01-07 | **CERRADA TÉCNICAMENTE Y ACEPTADA**: 8/8; PR #28 fusionado a dev; CI integral postmerge #42581697460 SUCCESS, C01-07 postmerge #42581697450 SUCCESS, A/B/C integrales PASS, propietario aprobó 2026-11-09. **C01 CERTIFICADO; 1/37**. |

**Repositorio:** PR #10 contiene código adelantado de fases 02–07 y permanece en borrador. Se integra solamente el alcance documental de C01-01 mediante un PR independiente, sin adelantar implementación privada ni certificar software.

**Infraestructura informada, NO VERIFICADA:** MySQL en VPS Hostinger y dominio `crecisol.com` posiblemente provisionados; esta etapa solo documenta una futura conexión externa parametrizada. Ni credenciales, ni configuración de DNS, ni conexión al VPS, ni despliegue productivo.

### Cierre técnico delegado de C01-01

C01-01 conserva **55 identificadores** (34 párrafos y 21 filas de tablas) y registra otras dos tablas suplementarias. **Tres auditorías diferenciadas PASS** en la CI de solo lectura: https://github.com/solqaryn-platform/CRECISOL/actions/runs/42520142623. El usuario autorizó verificaciones y cierres rutinarios sin nueva consulta; PR documental #11 queda autorizado para integración en `dev` una vez aprobado el commit final. **Progreso: 1/8 microfases cerradas, 0/37 capítulos certificados**; C01-02 es la siguiente fase por dependencia. Esto no certifica software productivo ni autoriza tocar Hostinger.

### C01-02: evidencia y cierre delegado

[C01-02 · Contratos](c01/C01_02_CONTRATOS.md) y [acta de microfase](c01/C01_02_ACTA_MICROFASE.md). El Domain .NET 10 compiló con **0 errores y 0 advertencias**, se ejecutaron **51 aserciones** y las tres auditorías A/B/C aprobaron en [CI #42521639367](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42521639367). Se comprobó la CI final [#42521796435](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42521796435), merge [#13](https://github.com/solqaryn-platform/CRECISOL/pull/13) en dev y CI post-merge [#42521854143](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42521854143). **C01-02 cerrada, 2 de 8 microfases concluidas; siguiente C01-03; 0/37 capítulos certificados.** Ningún endpoint, base de datos, panel ni infraestructura fue activado.

### C01-02 — cierre integrado a dev

La microfase C01-02 quedó fusionada con SHA `f0992d659e2a9535649d7a9cb0596d8ec9927479`, tras compilar el dominio net10.0 sin errores, ejecutar 51 pruebas y superar tres auditorías específicas A/B/C en CI. La CI post-merge de dev también aprobó. Los contratos no crean autorización de tenant ni servicios operativos; las obligaciones P0 de producto permanecen sin desplegar. [Acta verificable](c01/C01_02_ACTA_MICROFASE.md). La próxima microfase en secuencia es C01-03; el capítulo sigue sin certificación.

### C01-03 — pruebas reales y cierre técnico condicionado

El PR #15 incorporó el DDL inicial a dev antes de las pruebas, sin certificación. La corrección quedó en [PR #16](https://github.com/solqaryn-platform/CRECISOL/pull/16), con [CI real #42541568404](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42541568404): MySQL 8.4.13 aislado (9 positivos, 22 negativos), .NET10 Domain (51 aserciones) y triple auditoría A/B/C PASS. [Acta técnica](c01/C01_03_ACTA_MICROFASE.md). CI final del último HEAD y merge de PR #16 verificados: [#42541904259](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42541904259), merge `42c81b30347d43c718813a32df95b0ceb5566158`, y [post-merge #42542009481](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42542009481). C01-03 **CERRADA**, 3/8 microfases. La integración real proveedor MySQL/.NET, auth, inmutabilidad y validación de tenant quedan en C01-04+. No se certifica C01 ni se empieza C02: **0/37**.

### C01-04 — implementación aislada en revisión

Rama `feature/c01-04-public-brand-api`, partiendo de `dev` SHA `abf0351ff4400eb1b999abaf5659549cfacbe3fa`. Alcance: puerto y caso de uso de identidad pública, adaptador MySQL read-only, único GET de marca PLATFORM publicada y pruebas SQL/HTTP negativas con contenedor MySQL 8.4 descartable. [Contrato y límites](c01/C01_04_API_PUBLICA.md). No se integra PR #10, ni se inicia C01-05. Cierre de C01-04 condicionado a build real net10, pruebas positivas/negativas, auditorías A/B/C, acta y PR revisado. El capítulo C01 permanece **0/37 certificados**.

**C01-04 validación:** [CI real #42556858527](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42556858527) y [acta técnica](c01/C01_04_ACTA_MICROFASE.md), verificados contra MySQL 8.4.13 y API net10. El primer run falló al limpiar un volumen temporal (corregido); el último pasó 3/3 jobs. Cierre verificado: [CI final #42557022367](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42557022367), [PR #18 fusionado](https://github.com/solqaryn-platform/CRECISOL/pull/18) a SHA `9addbedd3c33c77c9be15fe25a391f9167a05ef5` y [post-merge #42557167001](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42557167001). C01-04 CERRADA técnicamente (4/8); C01-05 habilitada como siguiente microfase sin saltar pruebas. C01 sigue NO CERTIFICADO, 0/37.

### C01-05 — implementación y evaluación aisladas

Rama `feature/c01-05-angular-public-identity`, partiendo de `dev` SHA `da52bb0d29694c81b522f8f0668da698808db963`. [Especificación Angular, trazabilidad y riesgos](c01/C01_05_INTERFAZ_ANGULAR.md). C01-04 ya estaba cerrada. Implementación limitada a sitio público desde snapshot publicado y estados seguros; no introducir logo binario hasta C01-06. Puerta pendiente: build real Angular 22/Node 24.15/TypeScript 6, Chromium responsive/WCAG 2.2 AA y XSS/tenant, tres auditorías A/B/C, acta y merge individual. **No iniciar C01-06 sin ese cierre ni certificar C01 completo: 0/37.**

**Validación de C01-05:** [CI #42558658894](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658894) SUCCESS en build Angular, Chromium 11/11 y auditorías A/B/C; [foundation #42558658881](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558658881) SUCCESS. [Acta](c01/C01_05_ACTA_MICROFASE.md). El HTML continúa noindex y el logo gráfico sigue exclusivo de C01-06. Cierre confirmado en [CI final #42558828236](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558828236), merge [PR #20](https://github.com/solqaryn-platform/CRECISOL/pull/20) SHA `b902193b954d29f22dbdf8da9b22e48680f45dcd` y [CI post-merge #42558999820](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42558999820). **C01-05 CERRADA: 5/8. C01 sigue 0/37 capítulos certificados.**

### C01-06 — cierre técnico del logotipo oficial y header

El propietario ratificó el PNG original en las capturas de la portada y solicitó corregir el header textual. Se implementó el símbolo oficial azul recortado sin alterar los píxeles junto al nombre CRECISOL; el logo completo quedó en el hero con fallback seguro, favicon local y separación de marca de plataforma/tenant. Acta y auditorías A/B/C finales en [C01-06 · Acta de microfase](c01/C01_06_ACTA_MICROFASE.md). **PR #24 fusionado en dev** como `ac66a8aa39354d5126c3102be99e746452d455ce`, [CI final del último HEAD #42573961789](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42573961789) SUCCESS y [CI post-merge C01-06 #42574147302](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42574147302) SUCCESS, además de foundation pre/postmerge. **28/28 Chromium + 12 negativas del extractor + A/B/C FINAL PASS.** C01-06 **CERRADA TÉCNICAMENTE**, 6/8 microfases C01, 0/37 capítulos certificados. C01-07 queda habilitada para regresión/security/integración, no se autoriza C02 sin aceptación expresa de C01-08.


### C01-07 — cierre técnico de regresión real plataforma/tenant

Se integró [PR #26](https://github.com/solqaryn-platform/CRECISOL/pull/26) exclusivamente a `dev`, merge `745278d5ad3838ad5d63e0a15a88f6d9bafaeb9d`. CI del último HEAD `5477d07aa8911b0e2a776abf27f0c94248b022ba` [#42576150576](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576150576) y foundation [#42576150786](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576150786) SUCCESS. CI post-merge del código [#42576652049](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576652049) y foundation [#42576652052](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42576652052) SUCCESS. MySQL 8.4.13 desechable + API .NET 10 real + Angular 22/Node24: 37 solicitudes HTTP, 7 casos Chromium de integración, 20 Chromium de regresión C01-05/06, 51 aserciones de contratos .NET, 19 de Application y auditorías finales A/B/C PASS. Negativas de host, tenant, borrador, XSS, CORS, rutas privadas inexistentes, SQL de solo lectura, caché y ausencia de DSN. Acta [C01_07_ACTA_MICROFASE.md](c01/C01_07_ACTA_MICROFASE.md). **C01-07 CERRADA TÉCNICAMENTE: 7/8; C01-08 siguiente exclusivamente para auditorías integrales y conformidad expresa. C01 0/37 capítulos certificados; C02 no habilitado.**

### C01-08 — auditoría integral de capítulo y editabilidad futura

Abierta solo después del cierre C01-07. [Acta de auditoría C01-08](c01/C01_08_AUDITORIA_Y_ACTA.md) y [matriz transversal de parametrización](c01/C01_08_PARAMETRIZACION_TRANSVERSAL.md). Requisitos de marca, lectura pública versionada, seguridad y calidad se revalidan con regresiones reales. Corrección del header y favicon fijos para consumir solo snapshot validado. La interfaz administrativa universal sigue en capítulos posteriores y NO es funcionalidad implementada. Cierre técnico exige CI sobre último HEAD, auditorías integrales A/B/C, merge/CI postmerge y acta; CERTIFICACIÓN del capítulo exige además aprobación humana explícita. Hasta entonces C01-01...07 cerradas 7/8; 0/37 capítulos certificados; C02 NO HABILITADO.
