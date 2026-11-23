# CRECISOL · C05 UX · Fase 07 — Regresiones y evaluación visual integral

**Fecha:** 23-11-2026. **Estado inicial:** EN VALIDACIÓN; no certificar hasta que las CI y los tres dictámenes A/B/C terminen realmente en el mismo SHA final. **Objeto:** cerrar la ronda UX C05 F01–F06 ya aprobada en la candidata, sin iniciar ni certificar C06.

## 1. Identidad, procedencia y bloqueo de cambios
- Repositorio oficial verificado: `crecisol-platform/CRECISOL` (ID 1450414592; origen `solqaryn-platform/CRECISOL` es solo una ruta histórica redirigida). Skill y `AGENTS.md` vigentes en `dev`; `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md` y plan de 37 capítulos revisados.
- **BASE INMUTABLE, F06 certificada en candidata:** `96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf` / PR #186 fusionado. Workflow real F06 [#45265007952](https://github.com/crecisol-platform/CRECISOL/actions/runs/45265007952) **2/2 SUCCESS**, con 447 Chromium, 340 integración real Angular22→.NET10→MySQL8.4 y 18 migraciones/18 reversiones. Inventario [#45265008041](https://github.com/crecisol-platform/CRECISOL/actions/runs/45265008041) **SUCCESS**.
- Artifact original 65×5: [#14862391857](https://github.com/crecisol-platform/CRECISOL/actions/runs/45265008041/artifacts/14862391857), `c05-f06-inventory-65-96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf` (**439 PNG+JSON** y resumen). No existe consentimiento para reemplazar esta procedencia con capturas de otro SHA o archivo fabricado.
- Rama aislada F07: `feature/c05-ux-f07-regresiones-integrales-20261123`; destino permitido: `feature/c05-ux-f06-mensajeria-estados-20261120`. **Solo cuatro clases de archivos:** auditorías QA F07, casos negativos QA F07, comparador PNG F07, workflow F07 y esta acta (cinco rutas exactas). `frontend/src/`, `backend/`, `database/`, MySQL persistente, dependencias, `dev`, `main`, producción, SOLQARYN y C06: **CERO MODIFICACIONES**.
- Riesgo de discrepancias: se exigirá ancestor SHA F06, historial de **cada commit** y diff final. Cualquier archivo de producto que cambie, aun si después se revierte, hace fallar F07. Revisión visual de cambios a producto deberá abrir nueva intervención autorizada y volver a ejecutar F07.

## 2. Puertas comprobables y entregables

| Evidencia | Gate duro | Limitación registrada |
| --- | --- | --- |
| A — trazabilidad de las 65 interfaces | 65 IDs sin duplicados; matrícula de F06 con 65 dictámenes individuales IA ≥9.5; árbol de producto idéntico al SHA base | La CI no genera una calificación estética nueva |
| B — seguridad y privacidad | 9 tests de control positivos/negativos F07; ámbito de permisos F06 intacto; navegador y E2E real con negativas de tenants, cookies, sesiones, MFA y recuperación | Prueba con autenticadores físicos y restauración SQL+llaves persistentes PR #172 es independiente |
| C — UX, accesibilidad y rendimiento | Angular22/Node24 compilación, 447 pruebas Chromium originales, modales 55+165 capturas, 65×5 inventario, axe, foco, reduced-motion y semántica de errores | Sin lector de pantalla humano ni dispositivos reales |
| Comparación visual efectiva | Reejecutar las escenas 65×5 en SHA de F07, descargar artefacto F06 aprobado y comparar **cada uno de los 439 PNG** (dimensiones, ID, ruta, roles, contenido, estados y tolerancia de píxeles fija) | Tolerancia máxima: 5% de píxeles contrastados a ¼ de escala y diferencia media ≤10/255. Una diferencia que supere umbral = NO-GO |
| Matriz de aceptación de F07 | Reporte `visual_diff_report.json` + `F07_MATRIZ_FINAL_65.csv`, 65 filas, cinco resoluciones cada una, nota IA conservada y fallo individual si hay drift | `humanAcceptancePerformed=false`; la evidencia no otorga permisos del backend |
| Integración API y MySQL real | Angular→API .NET10→MySQL8.4 en contenedor desechable, 340/340 comprobaciones, 18 migraciones ida +18 reversión, sin tablas residuales | No hay despliegue ni pruebas productivas |

La evaluación se rige por **un mínimo individual de 9.5/10 sin promedio compensatorio**. Las notas IA de F06 (43×9.5 y 22×9.6) solo pueden ratificarse en F07 cuando el árbol fuente permanece idéntico, la comparación por ID/viewport pasa y no hay fallos P0/P1. Este criterio da **revalidación visual por comparativa SHA**, no una nueva auditoría estética humana.

## 3. Auditorías y plan de reversión
- **A:** existencia del inventario F01 reconciliado, contratos, 65 calificaciones justificadas, migraciones y rutas de scripts completos; diff/ancestry SHA exactos.
- **B:** evidencia E2E negativa de autorización y aislamiento, no aceptar secretos expuestos, datos sintéticos, no sobrescribir/compartir los ámbitos negocio/operadores, forma fail-closed.
- **C:** accesibilidad, mensajes, estados, animación reducida, no recortes ni overflow, contraste visual sin diferencias no revisadas, 65 filas con nota IA mínima.
- **Rollback:** revertir solo los cinco archivos F07 en rama candidata. No se ha tocado base de datos ni contratos; las ejecuciones Docker son efímeras.
- **Gates de salida:** CI F07 final de la rama/PR **3/3 SUCCESS sobre mismo SHA**, auditores A/B/C, matriz final y capturas originales adjuntas, **CI POSTMERGE sobre SHA final** y aceptación editorial IA acotada. Después actualizar este registro solo con evidencia comprobada o registrar cierre en comentario de PR sin falsificar un nuevo SHA.
- **Límite institucional:** certificación F07 (si procede) solo en código y QA de UX C05 de la candidata. PR #172 y #179 conservan sus propias aprobaciones y aceptación institucional; `dev`, `main`, despliegue, C06 y cobros siguen fuera de alcance. No declarar aceptación humana ni hardware que no se ejecutó.

## 4. Resultado en esta edición
**PENDIENTE DE RESULTADOS REALES DEL HEAD F07**. La evidencia PREVIA de F06 constituye la base comparativa, no un PASS anticipado de F07. No ocultar fallos de workflows históricos F03/F04 que puedan ejecutarse fuera de su alcance; las regresiones F03/F04 se repiten dentro del gate completo de F07.
