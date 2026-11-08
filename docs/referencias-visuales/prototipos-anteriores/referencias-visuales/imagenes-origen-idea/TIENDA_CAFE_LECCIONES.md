# Cumbre Café — Lecciones de la idea original

**Origen revisado:** `tienda-cafe.rar` (aproximadamente 85 MB, 26 024 entradas). Este documento rescata únicamente los conceptos observados al inspeccionar, sin ejecutar, los archivos propios de la aplicación.

## Flujos originales que sirvieron como punto de partida

1. **Tienda pública:** portada y catálogo con categorías, búsqueda, filtros, ordenamiento, fichas de productos y carrito lateral.
2. **Pedido y seguimiento:** checkout simulado con folio; lista de pedidos con estados e inventario visible para la administración.
3. **Administración operativa:** resumen con indicadores, artículos, stock bajo, gestión de productos, categorías y pedidos.
4. **Personalización visual:** edición de nombre y eslogan, colores, tipografía, radio y densidad; presets y vista previa.
5. **Experiencia responsive:** tienda orientada al consumidor y panel administrativo en rutas diferenciadas, con estados vacíos y confirmaciones.

## Valor para CRECISOL

La intuición original fue unir **presencia comercial, operación y personalización** sin obligar al negocio a manejar varias aplicaciones inconexas. Son patrones que sí conviene evaluar en los capítulos de catálogo, pedidos, constructor visual y experiencia del producto. El capítulo 01 se limita a establecer la identidad y el contrato de marca; estos flujos no se implementan por anticipado.

## Problemas detectados en el archivo original

- Usa React 18/Vite + Express 5/SQLite, distinto de Angular 22 + ASP.NET Core 10 + MySQL 8.4 LTS aprobados para CRECISOL.
- El usuario y la contraseña de administración están definidos literalmente para demostración en el frontend; las rutas POST/PATCH/DELETE del backend que se inspeccionaron no verifican identidad ni permisos.
- La base local es una sola SQLite sin aislamiento multitenant ni autorización por recurso; el descuento de inventario durante el checkout se ejecuta de forma no transaccional.
- Incluye `node_modules`, ejecutables y artefactos compilados, un archivo de base de datos con pedidos y una hoja de cálculo ajena al objetivo de inspiración. Publicar el RAR entero generaría ruido y exposición innecesaria.
- Las fotografías, marcas y textos de «Cumbre Café» no se incorporan automáticamente a CRECISOL; comprobar derechos si algún recurso específico fuera requerido.

**Decisión:** conservar este resumen conceptual y no subir ni ejecutar el RAR original. No usar su código como plantilla productiva ni sus credenciales o datos.
