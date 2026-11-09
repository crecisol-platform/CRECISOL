# C03 · Arquitectura técnica limpia y dependencias — microfases propuestas

> **C03-01 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE (1/10), formalización pendiente de CI PREMERGE/POSTMERGE del acta; C03 NO CERTIFICADO, C04 BLOQUEADO.** [Acta C03-01](../c03/C03_01_ACTA_MICROFASE.md) con 7/7 CI POSTMERGE del PR técnico #49. C02 fue certificado con aceptación humana «APROBADO» (9 noviembre 2026) y [12/12 CI finales](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42685252655). El [Word canónico](../MASTER_REFERENCE.md) fue leído de sus bytes originales con SHA-256 fijo y delimitación C03/C04, mediante [CI C03-01 #42687641345](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42687641345): **7 unidades originales y una tabla original de 9 filas**, más anexos. Las **8 puertas normativas MAE-C03-01..08** no equivalen a estas **10 microfases técnicas provisionales C03-01..10**; [especificación C03-01](../c03/C03_01_ESPECIFICACION.md). Este plan derivado **no modifica ni sustituye el Documento Maestro Único** y habilita planificar C03-02 solo tras completar la CI del acta, pero **C03-02 NO INICIADA**; C04 bloqueado.

**Puerta de entrada:** C02 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C03 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** Límites Domain/Application/Infrastructure/API; monolito modular salvo ADR.

## Plan secuencial de 10 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C03-01 | Mapa de módulos y dependencias | Grafo de límites permitidos documentado | No acoplamientos circulares |
| C03-02 | Contratos Domain puros | Entidades y reglas sin dependencias de frameworks | No instalar paquetes de infraestructura en Domain |
| C03-03 | Casos de uso y puertos Application | Un contrato probado por operación | No acceso directo de Application a MySQL |
| C03-04 | Adaptadores Infrastructure | Implementación de puertos aislada y reemplazable | No resolver credenciales desde peticiones |
| C03-05 | Convenciones API y DTO | HTTP y validación de entradas con esquemas tipados | No exponer entidades o secretos por serialización |
| C03-06 | Módulos verticales y DI | Registro explícito y pruebas de dependencias | No Service Locator ni dependencias fantasma |
| C03-07 | Workers y contratos de eventos | Idempotencia y outbox diseñados/probados en local | No ejecutar dos veces una acción por reintento |
| C03-08 | Configuración y ambientes locales | Variables tipadas y fail-closed sin datos reales | No .env secreto versionado ni conexión productiva |
| C03-09 | Compatibilidad y rendimiento basal | Build Angular/.NET/MySQL real, métricas repetibles | No afirmar compatibilidad por documentación sola |
| C03-10 | Auditorías integrales y acta C03 | A/B/C, regresiones, rollback y aceptación del capítulo | No abrir C04 sin certificación de C03 |

## Contrato obligatorio para C03-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c03/C03_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c03-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 10 microfases **no** certifica C03. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C04. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.
