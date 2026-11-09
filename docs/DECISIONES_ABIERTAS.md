# Decisiones documentales y arquitectónicas abiertas

- **ADR-001 — ABIERTA:** monolito modular inicial; extraer microservicios solo con mediciones y ADR aprobado.
- **ADR-002 — ABIERTA:** seleccionar y validar proveedor EF Core para .NET 10 y MySQL 8.4; ejecutar pruebas de migración e integridad.
- **ADR-003 — ABIERTA:** resolver SSR/híbrido Angular según rendimiento medido y alojamiento futuro.
- **ADR-004 — ABIERTA:** modelar routing de tenant dedicado, cutover, consistencia, rollback y recuperación sin fugas.
- **ADR-005 — ABIERTA:** validar titulares jurídicos, copyright, emisor de documentos y contratos reales; no inventar información legal.
- **ADR-006 — ABIERTA:** definir medios de pago, PSP y cumplimiento legal solo cuando la fase correspondiente esté autorizada.
- **ADR-007 — ABIERTA:** presupuestos de rendimiento y cuotas por plan tras benchmarks reproducibles.
- **ADR-008 — NO BLOQUEANTE PARA CODIFICAR:** protección de ramas en repositorio privado sin activación verificada; ver `docs/REPOSITORY_GATES.md`. No comprar servicios ni publicar el repositorio para intentar activarla.
- **ADR-009 — CERRADA DOCUMENTALMENTE (2026-11-08):** Documento Maestro Único institucional con logotipo incorporado y sus formatos Word/PDF verificados contra el contenido binario cargado en GitHub; tamaños y huellas en `docs/MASTER_REFERENCE.md`. La reconciliación de ramas se realiza preservando los objetos binarios nuevos y eliminando referencias a versiones antiguas. No implica software implementado ni certificado.

- **ADR-010 — ABIERTA / NO BLOQUEANTE:** VPS Hostinger/MySQL y dominio son destinos reportados, no verificados. Se prepara la configuración backend con secretos fuera del repositorio; el acceso y el despliegue requieren autorización futura.
- **ADR-011 — RESUELTA OPERATIVAMENTE PARA C01-01:** colisión de identificadores del contrato maestro y las ocho microfases técnicas. Se preservan ambos, usando MAE-C01-xx para referencias del maestro y C01-xx para fases técnicas; la [equivalencia](c01/C01_01_DEPENDENCIAS_Y_GATE.md) incluye ocho pasos. El usuario delegó comprobaciones y cierres rutinarios por microfase, sujetos a tres auditorías y CI. El Word no se modifica. C01 completo sigue requiriendo auditorías integrales, acta y aceptación.

**Estado actual de software: 1/37 capítulos certificados (C01; 9 noviembre 2026).** Certificación limitada a C01 conforme a `docs/c01/C01_08_AUDITORIA_Y_ACTA.md`, aceptación expresa y GitHub Actions postmerge. C02–C37 pendientes. Mantener abiertas las decisiones técnicas pendientes; no inferir preparación productiva, compras, despliegues ni reglas GitHub activas.
