# CRECISOL — Capítulo 04 · Preparación y control de entrada

**Estado actual:** capítulo **C04 NO IMPLEMENTADO / NO CERTIFICADO**; únicamente **C04-01 EN PREPARACIÓN**; C04-02..12 BLOQUEADAS, C05 BLOQUEADO. No confundir especificación con producto desarrollado.

**Repositorio operativo:** `crecisol-platform/CRECISOL`, rama `dev`. La propiedad original del repositorio no ha cambiado (GitHub ID `1450414592`). No modificar SOLQARYN ni recursos productivos.

## 1. Puerta C03 satisfecha

**C03 CERTIFICADO (10/10), 3/37 capítulos**, ratificado en [PR #81](https://github.com/crecisol-platform/CRECISOL/pull/81#issuecomment-6812682477), `dev` SHA `30c976e1db9add6ea00b07549c34f3c50d98dc31`, 6/6 PREMERGE ejecutadas en SUCCESS, 17/17 POSTMERGE SUCCESS, sin fallos, con auditorías A/B/C sobre SHA real. Las ejecuciones antiguas de PR #79 siguen en cola y no se les atribuye éxito. Este cierre **habilitó únicamente la preparación de C04-01**.

## 2. Autoridad y numeraciones

**Fuente única:** [Documento Maestro](MASTER_REFERENCE.md) Word/PDF, cuya identidad criptográfica debe verificarse antes de usar como autoridad funcional. [C04-01 especificación](c04/C04_01_ESPECIFICACION.md), subordinada a esa autoridad. [12 microfases propuestas](planificacion-futura/C04_MICROFASES_PROPUESTAS.md), sujetas a cotejo con los requisitos normativos `MAE-C04`; las 12 microfases técnicas no equivalen automáticamente a capítulos o puertas adicionales.

**Límites C04-01:** catálogo confiable (estados, nombres, referencias de direcciones sin DSN), no resolver host arbitrario, no seleccionar DB por cliente, no revelar secretos, y solo preparación de los contratos. El control de identidad/tenant real se implementará en fases posteriores con fail-closed.

## 3. Plan de ejecución, una sola microfase

| Gate | Entrega y evidencia | Estado |
| --- | --- | --- |
| P0 | Verificar Word/PDF, extracto fuente de capítulo 04, matriz de impacto y especificación contra el Maestro | EN PREPARACIÓN |
| P1 | Contrato de estados/identidad, autorización de plataforma y roles; DSN no público | DOCUMENTADO, NO IMPLEMENTADO |
| P2 | Riesgos T01–T18 y responsabilidades por microfase | DOCUMENTADO; negativos runtime NO EJECUTADOS |
| P3 | Auditorías preparatorias A/B/C mediante workflow sólo lectura y SHA exacto | PENDIENTE DE CI PROPIA |
| P4 | Acta de preparación con diff, fuente, hallazgos y aceptación para diseñar implementación posterior | PENDIENTE |
| P5 | Construir, probar y certificar el catálogo real | **NO AUTORIZADO POR ESTA PREPARACIÓN** |

## 4. Calidad obligatoria para decidir implementación

- Auditoría **A**: Word vigente por SHA real, fuente C04 extraída, cobertura del alcance C04-01, relaciones con COM-001/009/010 y límites con C04-02..12.
- Auditoría **B**: denegación por defecto, prohibición de `Host` como permiso, no DSN/secretos en DTO, contratos negativos de spoofing/colisiones/claims/suspensión, y clasificación P0/P1.
- Auditoría **C**: Domain independiente, Application por puertos, Infrastructure sin DSN del navegador, API sin endpoint privado hasta auth, MySQL 8.4 compartido/dedicado planificado y Node24/Angular22 solo donde apliquen. Los tests QA documentales no sustituyen pruebas reales de multitenancy.
- Gate de fuente: si el Word íntegro no puede contrastarse, **NO GO** para implementar o certificar. C04-01 requiere verificaciones positivas y negativas de código en su etapa autorizada.
- Todo trabajo de código de C04-01 se mantendrá separado de este PR documental; sin C04-02 ni acceso cross-tenant.

## 5. Contratos y dependencias abiertas

- **ADR-002**: EF Core compatible con .NET 10/MySQL 8.4 (pendiente de prueba). **ADR-004**: router dedicated, cutover y rollback sin fugas (pendiente). No resolverlos por inferencia.
- Catálogo protegido, DNS/host verificables, pertenencia y autorización backend, estados de suspensión y auditoría de cambios; lo anterior es **diseño**, no código.
- 3 modalidades de dirección, plan/contrato/facturación comercial y su parametrización son **dimensiones separadas**. Sin precios, reglas fiscales o cobros activados.
- No modificar el Documento Maestro, las pruebas históricas C01-C03, `main`, proveedores o producción.

## 6. Reversibilidad y expediente

**Riesgos activos:** exposición de información cross-tenant (P0), uso de Host no confiable (P0), DSN derivado del cliente (P0), registros concurrentes duplicados (P1/P0 según impacto). Ninguno se declara mitigado en runtime.

**Rollback:** revertir únicamente el PR documental/QA de preparación por SHA si una auditoría falla o hay contradicción con el Maestro; preservar C03 certificado y el historial.

**Siguiente decisión operativa:** concluir únicamente la **preparación auditada** de C04-01; el estado de implementación y certificación C04-01 seguirá **PENDIENTE** hasta que exista código, pruebas reales y acta futura aprobada.
