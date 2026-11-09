# Fase 1 · Capítulo 01 — CRECISOL: identidad, visión, alcance y principios

**Estado:** EN DESARROLLO; C01-01, C01-02, C01-03 y C01-04 cerradas técnicamente (4/8); capítulo C01 NO CERTIFICADO, 0/37. Progreso: **0/37 capítulos certificados**. Fuente: edición canónica de `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y `docs/MASTER_REFERENCE.md`.

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
| C01-05 | UI Angular, responsive, accesibilidad y navegador; requiere C01-04 | PENDIENTE |
| C01-06 | Logo y su presencia visual verificados; requiere C01-05 | PENDIENTE |
| C01-07 | Pruebas positivas, negativas, seguridad y regresión; requiere C01-06 | PENDIENTE |
| C01-08 | Auditorías integrales A/B/C, acta, conformidad del capítulo; requiere C01-07 | PENDIENTE |

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
