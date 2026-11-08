# CRECISOL — Referencias visuales seleccionadas

**Fecha de revisión:** 8 de noviembre de 2026  
**Naturaleza:** archivo histórico de inspiración; **NO** es Documento Maestro, código productivo, especificación normativa, aprobación de pruebas ni certificación.

## Fuentes seleccionadas

1. `prototipos-anteriores/Atlas_vision_general.png`: captura representativa de CRECISOL Atlas PRO. Se conserva por el shell con navegación lateral agrupada, encabezado contextual, portada editorial, tarjetas de indicadores, mosaicos de experiencias, panel de decisiones y tareas; no por sus cifras.
2. `prototipos-anteriores/Centro_calidad_01_07.png`: captura representativa del panel de auditoría de los capítulos 01 a 07. Se conserva como referencia de diseño para estados, hallazgos, trazabilidad, separadores, estructura de módulos, tarjetas de resumen y visibilidad de acciones pendientes.
3. `imagenes-origen-idea/TIENDA_CAFE_LECCIONES.md`: conclusiones extraídas por lectura estática de la implementación temprana «Cumbre Café» para recordar de dónde surgió la idea de una tienda adaptable. No se almacena su código ni su base de datos.

## Patrones de interfaz que sí aportan valor

- Un sistema de navegación coherente por experiencias y permisos; menú lateral agrupado, breadcrumbs y herramientas contextuales.
- Composición editorial: títulos claros, aire, agrupación de información, tarjetas y métricas legibles; no saturar pantallas.
- CRUD con búsqueda, filtros, estado vacío, confirmaciones, tarjetas y tablas fáciles de escanear.
- Constructor visual sobre **parámetros validados**: marca, tipografía, espaciado, densidad, colores, componentes y vista previa previa/posterior al guardado. La publicación y edición deben tener permisos y auditoría reales.
- Centro de QA que distinga **documentado, implementado, probado y certificado** mediante evidencia verificable, no solo números de una demo.
- Móvil y escritorio con estructura adaptativa; navegación accesible por teclado, buen contraste y estados de carga/error/éxito.

## Qué NO se hereda

- **No** se heredan las paletas verde/lima, iconos «C» ni el logotipo dibujado de los prototipos. Se impone el logotipo azul oficial aprobado de CRECISOL, sin redibujarlo.
- **No** se heredan cifras antiguas como 83, 38, 84 o 940 como resultados actuales; pertenecen a diferentes cortes documentales históricos.
- **No** se reutilizan HTML, React, Express, SQLite, librerías, credenciales de demostración, bases de datos, tokens, enlaces de documentación antiguos ni lógica de autenticación de prototipos.
- **No** se asumen aprobados los proveedores alternativos mencionados por prototipos viejos: el stack, permisos, servicios y fases los determina el Maestro Único vigente de CRECISOL.
- **No** se ejecutan scripts de origen desconocido, prototipos históricos ni archivos comprimidos como parte de la aplicación.

## Criterio de implementación

Cada futura pantalla se diseñará para una tarea de producto aprobada, se construirá desde cero con Angular 22 y backend .NET 10 donde proceda, deberá respetar aislamiento multitenant, accesibilidad, rendimiento y configuración por ámbito. Los prototipos solo sugieren **patrones**, nunca reemplazan requisitos ni pruebas.

**Fuente canónica:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y `docs/MASTER_REFERENCE.md`. **Estado de software:** 0/37 capítulos certificados salvo evidencia posterior. No existe permiso implícito para editar SOLQARYN.
