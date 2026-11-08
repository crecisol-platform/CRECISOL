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

**Estado actual de software: 0/37 capítulos certificados.** Mantener abiertas las decisiones técnicas pendientes; no bloquear la preparación de C01 por compras, despliegue o reglas GitHub indisponibles.
