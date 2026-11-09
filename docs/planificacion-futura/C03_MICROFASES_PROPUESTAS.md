# C03 · Arquitectura técnica limpia y dependencias — microfases propuestas

> **C03-01 CERTIFICADA; C03-02 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (2/10), sujeta a CI PREMERGE/POSTMERGE de su acta; C03 NO CERTIFICADO, C04 BLOQUEADO.** [Acta específica C03-02](../c03/C03_02_ACTA_MICROFASE.md), [acta C03-01](../c03/C03_01_ACTA_MICROFASE.md) y [Word canónico](../MASTER_REFERENCE.md). PR técnico #51 fusionado a `dev` SHA `adc975c7bcf4b6a772cc160a5b377d26af2b5fa6`, 3/3 CI POSTMERGE SUCCESS. C03-03 todavía **NO INICIADA**. La matriz real de C03 conserva siete unidades originales y una tabla original de nueve filas; las ocho puertas normativas `MAE-C03-01..08` no equivalen a las diez microfases técnicas propuestas `C03-01..10`. El plan no sustituye el Maestro.

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
