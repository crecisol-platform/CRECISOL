# CRECISOL · Decisión de curaduría y criterios de UI

**Auditoría documental:** 8 de noviembre de 2026. **Estado:** CERRADA para preparar la fase 1. **No constituye** certificación ni prueba de la implementación del SaaS.

## Inventario final aprobado

| Origen | Material activo | Propósito |
| --- | --- | --- |
| Tienda café | [7 imágenes](imagenes-origen-idea/README.md) | Cómo nació la idea: tienda, catálogo, gestión y apariencia |
| Tienda café | [Lecciones sintetizadas](imagenes-origen-idea/TIENDA_CAFE_LECCIONES.md) | Conceptos útiles, riesgos técnicos y exclusión de código heredado |
| CRECISOL Atlas | [Atlas · visión general](prototipos-anteriores/Atlas_vision_general.png) | Navegación, jerarquía y experiencias |
| Auditoría 01–07 | [Centro de calidad](prototipos-anteriores/Centro_calidad_01_07.png) | Hallazgos, estado, trazabilidad y evaluación |

## Revisión del HTML: ¿añade algo que no muestran las capturas?

**Sí, pero basta con conservar sus hallazgos y no su código.** Se inspeccionaron estáticamente los archivos originales aportados en esta conversación:

- `CRECISOL_Atlas_PRO.html`: contiene **15 vistas** de exploración (resumen, maestro, matrices, rubros, experiencias, constructor, flujos, seguridad, infraestructura, cobros, decisiones, trabajo, pruebas, diagnósticos y documentación). Muestra búsqueda global, filtros, lector de capítulos, menús, modales, edición y vista previa, y guardado/exportación local.
- `CRECISOL_Studio.html` y su ZIP: versión anterior del estudio de producto. Aporta variaciones de presentación, pero no elementos indispensables que no se encuentren en Atlas o las capturas seleccionadas.
- `CRECISOL_Auditoria_01_07_INTERACTIVA.html`: explora las matrices, hallazgos y pruebas **documentales** de los primeros siete capítulos. Sus resultados no se trasladan como evidencia de software certificado.

**Límite real:** el HTML utiliza datos de demostración y `localStorage` para conservar decisiones locales; no implementa un SaaS auténtico con backend, MySQL, control de usuarios y aislamiento multitenant. Las cifras de sus dashboards pertenecen a versiones históricas. No reutilizaremos sus scripts, credenciales, enlaces antiguos, librerías, modelo de autorización ni código en Angular.

**Decisión:** no subir los HTML ahora. La información visual y conductual útil queda registrada en este archivo. Si una futura microtarea requiere comprobar un comportamiento que no se haya descrito, se revisará el HTML original puntualmente y en modo lectura.

## Patrones que sí aprovecharemos

1. **Una experiencia, no pantallas aisladas:** navegación consistente según rol; menús con agrupaciones, encabezados contextuales, búsqueda, tablas, filtros y ayuda.
2. **Sistema visual propio:** logo azul oficial, tipografía legible, espaciado, jerarquías editoriales, iconografía uniforme y componentes adaptables. No adoptar el verde/lima ni el isotipo «C» provisional.
3. **Previsualización confiable:** estado borrador → vista previa → confirmación → publicación → comprobación posterior → reversión, siempre con configuración tipada y autorizada.
4. **Diseño para operaciones reales:** estados vacíos, carga, error y éxito; confirmación para acciones sensibles; edición de datos validada.
5. **Seis experiencias diferenciadas:** sitio público CRECISOL, registro/contratación, consola SuperAdmin, panel del negocio, puesto de empleado y portal del cliente. Atlas es un estudio de producto, no una séptima aplicación obligatoria.
6. **Seguridad transversal:** los menús y guards solo ayudan a la UX. Autenticación, autorización, tenancy, protección de archivos y acceso por acción los aplica el backend.
7. **Accesibilidad y rendimiento:** interfaces móviles y de escritorio, teclado, foco visible, lector de pantalla, WCAG 2.2 AA, reduced motion y pruebas medibles.

## Material retirado o excluido

- Se retiraron **3 capturas** del árbol activo: una de acceso con credenciales de prueba visibles y dos repeticiones parciales del catálogo/editor. La eliminación del árbol **no borra el historial Git**.
- No se incluye `tienda-cafe.rar`: contenía dependencias, una base SQLite, archivos de build, datos de demo y código React/Express ajeno al stack de CRECISOL.
- No se incluyen los archivos HTML ni el ZIP de Studio: pueden servir como lectura histórica, pero no como código base.
- Tampoco se heredan cifras históricas ni afirmaciones de que una interfaz de demostración esté implementada o auditada.

## Regla de aplicación en capítulo 01

El **Documento Maestro Único** es la fuente obligatoria. En C01 usaremos únicamente lo pertinente a identidad: logotipo azul aprobado, lema, contenido parametrizado, separación marca plataforma/negocio, cabecera/pie, vista previa, tipografía y accesibilidad.

No implementaremos a destiempo pedidos, cobros, catalogación, auditoría productiva ni constructores completos de capítulos futuros. Para cada pantalla nueva se documentarán el criterio maestro, la referencia visual inspiradora, el diseño nuevo y las pruebas realizadas.

**Veredicto:** las imágenes más el análisis escrito son suficientes **para comenzar** a diseñar y programar desde cero. No es una garantía de superioridad estética sin revisión posterior; la calidad se comprobará con prototipos nuevos, pruebas y tu aprobación.

**Estado de software:** 0/37 capítulos certificados. **Áreas del código afectadas por esta curaduría:** ninguna. Solo documentación dentro de CRECISOL.
