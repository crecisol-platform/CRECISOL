# CRECISOL C06 — Dictamen independiente de preparación para C06-01

**Fecha de revisión:** 2026-11-24. **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base de `dev`:** `659477eef45780d5b36ffac95162075dd0aded24`. **Acción solicitada por el propietario:** iniciar C06-01, sin pruebas en su equipo ni desplegar.

## Condición institucional y alcance de la decisión

- La autoridad vigente de `docs/ESTADO_VIGENTE_CAPITULOS.md` certifica C05 **únicamente en desarrollo local aislado** y conserva C06 como BLOQUEADO hasta un dictamen de preparación e identidades confiables. No se modifica retroactivamente esa certificación.
- La última corrección de C05 (PR #191) fue fusionada en `dev` `659477eef45780d5b36ffac95162075dd0aded24`; CI POSTMERGE: **4/4 workflows SUCCESS** (C03, fundamentos, institucional y operativa Angular/.NET/MySQL), sin fallos ni pendientes en el SHA. El fallo Chromium previo quedó resuelto con 73 PASS y 1 SKIPPED en la ejecución PREMERGE.
- Documento Maestro Único en `dev`: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Git blob `fe7aaab60b603f1844e18666e2f99c0ea97e86e9` (1,353,343 bytes); SHA-256 registrado `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`. La prueba A deberá **leer realmente su capítulo 06** y comprobarlo, no sustituirlo por la propuesta histórica.
- La propuesta de 11 microfases en `docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md` sigue siendo **plan preliminar**; C06-01 aborda solo inventario y códigos de acción. No se inicia C06-02 hasta certificar C06-01.

## Puerta autónoma de entrada — decisión acotada

**Se permite preparar y revisar C06-01 en rama `feature/c06-01-*`, como contrato de inventario; no equivale a habilitar todavía políticas productivas de C06.** La aceptación delegada del propietario habilita el trabajo, pero la formalización del gate exige A/B/C reales, contraste con las 70 unidades de párrafo del Word y sus ocho acciones G.7 y CI en el SHA de la candidata.

- **A (funcional y trazabilidad):** inventario exhaustivo de endpoints que `Program.cs` registra en el entorno aplicable y lista de contratos no expuestos; métodos/rutas/operaciones sin inventos; trazabilidad al Word y al código.
- **B (seguridad, tenant y privacidad):** realm PUBLIC/TENANT/PLATFORM, contexto verificado por backend, rutas condicionales, ausencia de privilegios por etiquetas Angular, controles negativos contra omisión, duplicación, falsificación de tenant y exposición del endpoint de prueba.
- **C (arquitectura, compatibilidad y UX):** catálogo de datos único en backend, documentación de consumidores futuros; sin cambios en autorizadores existentes, roles, MySQL, exposición HTTP ni interfaces; compilación del frontend/backend cuando haya CI disponible.
- **No-go:** campos inventados como grant; rutas nuevas no catalogadas; un endpoint de arnés convertido en productivo; no poder comprobar el Word; fuga de tenant o P0/P1; respaldo de datos del propietario tocado.
- **Separación:** la autenticación y MFA local de C05 ofrecen evidencia suficiente para inventariar límites existentes; **no certifican** todavía la gestión de roles C06-02, ABAC C06-04 ni el SuperAdmin excepcional C06-06.

**Estado de este documento antes de la CI:** puerta de revisión abierta para **inventario solamente**, con certificación C06-01 condicionada a evidencia del PR. No se autorizan C06-02/C07, VPS, R2, producción, cobros, corte persistente de C05 ni SOLQARYN.

## Contraste del maestro completado en la rama candidata

La extracción auditada del capítulo C06 identificó expresamente: permisos por acción y RBAC+ABAC, separación financiera, privacidad campo por campo, soporte break-glass, delegación sin escalada, revocación, logs, condición comercial, protección de realtime/medios y ocho acciones universales G.7. Los 28 códigos normativos candidatos se separan de la superficie HTTP existente y **no se activan**. Se exige mantener siete controles TEN señalados en el maestro. Aceptación definitiva sujeta a CI en el HEAD y cierre en `dev`.
