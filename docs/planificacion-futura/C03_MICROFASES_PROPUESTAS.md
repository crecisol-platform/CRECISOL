# C03 · Arquitectura técnica limpia y dependencias — microfases propuestas

**ESTADO PROSPECTIVO DE CERTIFICACIÓN (11-11-2026):** Las diez microfases C03 se sometieron a la auditoría final A/B/C; [acta integral](../c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md). La certificación institucional C03 (10/10), 3/37 y habilitación para PREPARAR C04-01 solo son efectivas después de la CI de cierre PRE/POSTMERGE, como fija [registro de estado](../ESTADO_VIGENTE_CAPITULOS.md). Los marcadores de C03 NO CERTIFICADO posteriores son históricos.

**ACTUALIZACIÓN POSTERIOR (11-11-2026):** la planificación fue ejecutada hasta C03-10: C03-09 certificada en su alcance y **C03-10 cerrada técnicamente** con las auditorías A/B/C y 16/16 CI POSTMERGE del PR #75 ([acta](../c03/C03_10_ACTA_CIERRE_TECNICO_CI.md)). **C03 NO CERTIFICADO; C04 BLOQUEADO**. Los estados históricos posteriores a esta línea no sustituyen el cierre vigente.

> **ESTADO VIGENTE: 2/37 capítulos certificados (C01 y C02); C03-01 a C03-08 CERTIFICADAS (8/10), C03-05 CERTIFICADA (5/10), C03-06 CERTIFICADA (6/10) y C03-07 CERTIFICADA (7/10) tras PR técnico #63 (8/8 CI POSTMERGE SUCCESS, SHA a256885afa5ee2f639bd4ed6f7d8595b709e6409) y PR documental #64 (5/5 CI POSTMERGE SUCCESS, SHA b1e019afabfb44f05076024b8d34cde74a340acc); C03-08 CERTIFICADA (8/10) por PR #70 (6/6 CI POSTMERGE SUCCESS, SHA 221afb6cc515fc79a3b6ca6e010af11f2e20b0a2) y PR #71 (5/5 CI POSTMERGE SUCCESS, SHA b333e58378b6a5ea0438f1ee6b11de85d9ce04df); C03-09 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO.** [Evidencia definitiva de C03-06](../c03/C03_06_CERTIFICACION_POSTMERGE.md), PR documental #61. [C03-05 previa](../c03/C03_05_CERTIFICACION_POSTMERGE.md). El párrafo siguiente registra exclusivamente el **histórico de 4/10**, no el estado vigente.

> **HISTÓRICO C03-04:** C03-01, C03-02 y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10), condicionada a CI PREMERGE/POSTMERGE del acta; C03-05 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO.** [Acta C03-04](../c03/C03_04_ACTA_MICROFASE.md), [acta C03-03](../c03/C03_03_ACTA_MICROFASE.md), [acta C03-02](../c03/C03_02_ACTA_MICROFASE.md), [acta C03-01](../c03/C03_01_ACTA_MICROFASE.md), [Word canónico](../MASTER_REFERENCE.md). PR técnico #55 `dev` SHA `d529f85f0bc8710940d195fceaea06bf6c5e674d`, 3/3 CI técnicas POSTMERGE SUCCESS; C03-03 [PR #54](https://github.com/solqaryn-platform/CRECISOL/pull/54) **9/9 CI POSTMERGE SUCCESS**. Las ocho puertas `MAE-C03-01..08` no equivalen a diez microfases `C03-01..10`. Esta propuesta no sustituye el Maestro, ni abre C03-05 o C04.

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
