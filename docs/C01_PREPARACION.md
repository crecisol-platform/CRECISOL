# Fase 1 · Capítulo 01 — CRECISOL: identidad, visión, alcance y principios

**Estado:** LISTO PARA INICIAR IMPLEMENTACIÓN; NO DESARROLLADO, NO PROBADO, NO CERTIFICADO. Progreso: **0/37 capítulos certificados**. Fuente: edición canónica de `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y `docs/MASTER_REFERENCE.md`.

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
