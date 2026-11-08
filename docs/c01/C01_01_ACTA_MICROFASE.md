# C01-01 · Acta técnica de microfase (no es acta del capítulo 01)

**Estado inicial:** ABIERTA, pendiente de tres auditorías GitHub Actions y merge al ámbito `dev`. Se actualizará después de comprobar la ejecución real. **No es certificación del software ni del capítulo.**

## Alcance en esta microfase
Matriz de origen P01–P34 (34 párrafos: 27 de contenido, 7 encabezados), T01–T03 (21 filas de datos, 24 incluyendo encabezados), anexos 2×3; registro de dependencias, riesgos, Hostinger no verificado y nombres MAE-C01-xx frente a C01-xx.

**Auditorías obligatorias y separadas:**
- **A — Trazabilidad:** lectura OOXML y SHA, 55 identificadores, comprobación de correspondencia de temas, pruebas negativas para faltantes y duplicados.
- **B — Seguridad/aislamiento documental:** solo documentos/QA/CI, sin backend/frontend/database, sin secretos o cadenas, sin VPS ni acceso productivo. No sustituye la auditoría de seguridad completa del SaaS.
- **C — Arquitectura/calidad de documentación:** enlaces internos, ocho dependencias, nomenclatura dual 8/8, estado y rollback, no mezclar código provisional.

**Puertas:** si falla cualquiera de las tres, NO se fusiona; corregir y volver a ejecutar. La autorización de usuario para cierres automáticos de microfases es expresa y se limita al alcance revisado. **C01-08 mantiene la aceptación del capítulo completo**.

**Rollback:** revertir el PR propio de esta microfase; no hay migraciones ni datos afectados.

**Resultado final:** se actualizará únicamente con CI verificada y estado real de merge. Ninguna certificación de capítulo emitida.
