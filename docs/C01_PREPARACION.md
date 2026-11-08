# Fase 1 · Capítulo 01 — CRECISOL: identidad, visión, alcance y principios

**Estado:** EN DESARROLLO; IMPLEMENTACIÓN PARCIAL; NO PROBADO INTEGRALMENTE, NO CERTIFICADO. Progreso: **0/37 capítulos certificados**. Fuente: edición canónica de `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y `docs/MASTER_REFERENCE.md`.

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

## Inicio de implementación — primer corte

Se creó un read-model de marca de plataforma versionado, una API pública de lectura, esquema relacional previsto y estructura Angular 22 con identidad azul y extracción del logotipo original. Son componentes iniciales sin endpoints de escritura ni resolución de tenant. Ver docs/c01/INICIO_Y_TRAZABILIDAD.md para alcance, riesgos y pruebas pendientes. La biblioteca visual es referencia, no código base. C01 sigue abierto.

## Verificación técnica parcial real

La compilación Angular 22 / .NET 10, el contrato HTTP inicial y las claves de MySQL 8.4 aprobaron en https://github.com/solqaryn-platform/CRECISOL/actions/runs/42516130198. Es evidencia técnica del primer segmento de C01, no certificación. Siguen pendientes la UI en navegador, la autorización y aislamiento multitenant, la seguridad integral y las tres auditorías. Ver [trazabilidad de C01](c01/INICIO_Y_TRAZABILIDAD.md).

## Gobierno operativo: ocho microtareas en orden obligatorio

**Acuerdo operativo de 2026-11-08.** El bloque C01 se divide en **8 microtareas, C01-01 a C01-08**, en orden ascendente y de dependencia. Una microtarea no se considera CERRADA por tener código escrito, compilación aislada o capturas; requiere requisitos trazados, evidencia pertinente, pruebas positivas y negativas aplicables, revisión y aceptación de su alcance. La **certificación institucional** del capítulo solo ocurre después de C01-08 con las auditorías A/B/C y aceptación expresa. No equivale a ocho certificaciones de producto independientes.

| Fase | Dependencia formal | Entregable / condición para cerrarla | Estado de cierre |
| --- | --- | --- | --- |
| C01-01 | Documento Maestro Único vigente | Matriz completa de los 34 párrafos y 21 filas fuente, dependencias, criterios verificables, riesgos y límites de C01 | **ACTIVA: sin cierre** |
| C01-02 | C01-01 cerrada | Contratos tipados plataforma/tenant, borrador, vista previa, publicación y validaciones; definir reglas sin habilitar edición insegura | Pendiente de cierre; existe código preliminar |
| C01-03 | C01-02 cerrada | Modelo MySQL 8.4, llaves, scopes, plan de migraciones, pruebas aisladas y estrategia de conexión parametrizada | Pendiente de cierre; DDL temporal ya probado |
| C01-04 | C01-03 cerrada | Domain/Application/Infrastructure/API, solo lectura de marca publicada y seguridad de rutas privadas por denegación | Pendiente de cierre; API pública preliminar ya compilada |
| C01-05 | C01-04 cerrada | Angular 22 de identidad, cabecera/pie, SEO, diseño responsivo, estados, teclado, accesibilidad y UI verificada en navegador | Pendiente de cierre; primer shell compilado |
| C01-06 | C01-05 cerrada | Cotejar de extremo a extremo el logotipo oficial original en todas las vistas de C01, sin alterar ni deformar activo | Pendiente de cierre; extracción por SHA ya aprobada |
| C01-07 | C01-06 cerrada | Pruebas positivas/negativas pertinentes de contratos, tenant/host, caché, XSS, permisos, regresión y rendimiento, con evidencia | Pendiente de cierre; pruebas técnicas parciales ya aprobadas |
| C01-08 | C01-07 cerrada | Auditorías A (trazabilidad), B (seguridad) y C (arquitectura/rendimiento/UI); corrección P0/P1, acta y aceptación del C01 | Pendiente |

**Regla de continuidad:** no iniciar trabajo nuevo de una microtarea posterior hasta cerrar la anterior. El código que se adelantó en el PR #10 se conserva únicamente como **implementación provisional que deberá revalidarse** cuando llegue su turno; no constituye cierre ni salta puertas. No abrir C02 sin acta C01.

### Hostinger, MySQL y dominio: destino previsto, estado no verificado

El propietario informó que **podría existir una base de datos en un VPS de Hostinger** y que **el dominio podría estar libre o ya configurado**. Son datos **reportados, no comprobados**. No afirmar existencia, compatibilidad, acceso, propiedad de dominio, TLS, registros DNS ni disponibilidad hasta realizar comprobación autorizada posterior.

- Preparar en código **configuración externa** para el host, puerto, nombre lógico de base, usuario de servicio, secreto, modo TLS y dominio público; no fijar direcciones ni contraseñas en código, commit o documento. Ejemplos de *nombres de parámetros*, sin valores: \`ConnectionStrings__CrecisolDb\`, \`Crecisol__PublicBaseUrl\`, \`Crecisol__AllowedHosts\`.
- La conexión MySQL **solo corresponde al backend**, bajo control de identidad, tenant y permisos. El frontend nunca recibe una cadena de conexión ni puede elegir DSN, host, base o tenant.
- La futura conexión al VPS **no consiste solamente en tres líneas de texto**: exige compatibilidad de proveedor .NET/MySQL, TLS, permisos mínimos, esquema/migraciones validadas, seguridad de red, respaldo, rollback y controles de tenants shared/dedicated.
- Utilizar base MySQL local/temporal con datos ficticios en la fase actual. No conectarse a VPS, importar producción, modificar DNS, desplegar ni activar servicios externos sin nueva decisión y autorización expresa del usuario.
- En C01-03 se prepara el contrato de configuración y migración **sin credenciales reales**. La activación de Hostinger y el dominio queda fuera del alcance actual y no es bloqueo para desarrollar C01.

### Formato obligatorio de reporte por avance

Informar siempre: capítulo N/37, capítulos certificados; número total de microtareas de C01; cerradas/activas/pendientes; microtarea actual y la siguiente; rama y PR (borrador/abierto/fusionado); pruebas efectivamente ejecutadas, hallazgos y riesgos; estado de integración a dev/main y prohibición de pasar a C02 antes del acta. **Estado inicial del control secuencial: 0 de 8 microtareas con cierre formal; activa C01-01.** El trabajo preliminar existente mantiene sus evidencias parciales.
