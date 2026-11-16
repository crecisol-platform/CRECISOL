# C03 — Reauditoría retrospectiva de calidad y coherencia con C01/C02

**Fecha:** 16-11-2026. **Repositorio autorizado:** `crecisol-platform/CRECISOL`. **Rama propuesta:** `feature/c03-retro-quality-20261116` → `dev`. **Base comprobada:** `18453576533641cbe1663db02930ccaeae7f18a3`. **Estado:** CORRECCIÓN PROPUESTA, **NO CERTIFICADA** hasta evidencia real, triple auditoría y decisión de integración.

## Alcance y motivo

Por instrucción expresa del propietario, la revisión correctiva se ejecutará **C03 → C04 → C05**, un solo capítulo por vez. Este expediente modifica **exclusivamente los controles arquitectónicos y la documentación de estado asociados a C03**. No reabre la implementación de C04/C05, no altera sus fuentes ni las certificaciones históricas, y no inicia C06.

C03 define la arquitectura limpia, el grafo de dependencias, configuración local, DI, outbox sintético, compatibilidad y pruebas. **No tiene pantallas de negocio propias que deban rediseñarse**: la coherencia visual aquí exige proteger los contratos compartidos de C01/C02 (fuente de marca, contraste/foco, reducción de movimiento, skip-link y navegación lazy), no inventar otro panel. La aceptación estética de C04/C05 requiere inspección renderizada posterior y nunca se infiere de las pruebas de C03.

## Incidencias reales encontradas

1. **C03-RQ-01 · Lectura institucional contradictoria (P2, confirmada).** El encabezado del README aún decía que C03 no estaba certificado, C04 estaba bloqueado, solo existía el GET público y no había identidad privada. Son datos de una instantánea del 11-11-2026; `docs/ESTADO_VIGENTE_CAPITULOS.md` registra posteriormente C01–C05 certificados en su ámbito acotado. **Remediación:** agregar síntesis operacional verificable y rotular los inventarios viejos como históricos sin reescribirlos.
2. **C03-RQ-02 · Detector de dependencias sensible a artefactos de compilación (P2, confirmada en lectura).** `qa/c03_01_source.py:actual_graph()` recorría todos los `*.cs` del Domain y Application, también los generados en `obj`/`bin`. El auditor Domain C03-02 ya excluía esos directorios, dejando una divergencia entre controles. **Remediación:** helper `authored_cs_files` y nueva regresión negativa con archivos simulados en ambos directorios.
3. **C03-RQ-03 · Protección contra nuevo desfase (P2, mejora de control).** El gate C03-10 validaba actas históricas correctamente, pero no comparaba específicamente el estado operacional de primer plano del README con el registro vigente. **Remediación:** nuevo auditor `qa/c03_10_current_state_audits.py`, A/B/C, cinco mutaciones adversariales y ejecución junto al gate integral de runtime existente.
4. **C03-RQ-04 · Calidad visual completa (pendiente por alcance).** Se comprueba la presencia de convenciones CSS y rutas de C01/C02, pero **no se declara aprobación estética humana, WCAG manual ni capturas renderizadas de C03/C04/C05**. C04/C05 tendrán inspección de pantallas por etapas cuando corresponda.

## Impacto de arquitectura y seguridad

- **Frontend:** cero modificaciones en `frontend/`. Nuevas comprobaciones de CSS global, accesibilidad básica, dos páginas públicas y rutas lazy; no hay nuevo diseño ni excepciones de autorización.
- **Backend:** cero modificaciones en `backend/`, .NET, contratos o endpoints. Se continúa usando el grafo real de proyectos de C03 como prueba de límites Domain/Application/Infrastructure/API.
- **Datos:** cero cambios en `database/`, SQL, fixtures, transacciones, migraciones, rollback o permisos de tenants. C04 solo conserva su evidencia histórica de datos sintéticos.
- **CI y QA:** `qa/c03_01_source.py`, `qa/test_c03_portability.py`, `qa/c03_10_current_state_audits.py` y `.github/workflows/c03-10-integral.yml`; los nuevos A/B van en la primera etapa y C después de compilación .NET 10, Angular 22/Node 24, Chromium y MySQL 8.4 del workflow original. Los marcadores de ejecución se exigen por SHA/run exactos, sin suplantarlos.
- **Documentación:** README y este expediente. Documento Maestro DOCX/PDF, actas históricas y `main` inalterados.

## Auditorías A/B/C y puertas de cierre

| Puerta | Qué debe comprobar | Estado al proponer |
| --- | --- | --- |
| A · Funcional/trazabilidad | Resumen operacional C01–C05 vs. registro canónico, no confundir estados históricos, Word/PDF referenciados | **NO EJECUTADA aún en el SHA final** |
| B · Seguridad/límites | Grafo físico C#, Domain/Application limpios, 5 mutaciones de estado detectadas, sin cambio de superficies privadas ni privilegios | **NO EJECUTADA aún en el SHA final** |
| C · Arquitectura/UX y pruebas | CSS accesible C01/C02, navegación lazy, suite de regresión C03-10 .NET/Angular/Chromium/MySQL y portabilidad Windows/Linux; inspección renderizada cuando haya superficie visual nueva | **NO EJECUTADA aún en el SHA final** |

**NO-GO** si hay fallos obligatorios vigentes, discrepancia de SHA, cambios extra de alcance, secreto filtrado, bypass multitenant o resultados no ejecutados presentados como PASS. Esta corrección no extiende C03 a producción, MFA real productivo, pagos, administración global ni proveedores.

**Reversión:** cerrar el PR sin fusionar o revertir sus commits por SHA si ya hubieran ingresado a `dev`. No hay reversión SQL por no existir DDL modificado. Conservar evidencia histórica y registrar todo fallo de CI.

**Certificación:** solo informar integración/cierre tras validar los resultados PREMERGE y POSTMERGE en SHAs exactos, los tres dictámenes, ausencia de P0/P1 y el resultado de una revisión humana de las pantallas que realmente hayan cambiado.
