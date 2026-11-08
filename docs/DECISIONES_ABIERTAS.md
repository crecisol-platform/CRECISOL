# Decisiones documentales y arquitectónicas abiertas

- ADR-001: monolito modular propuesto por ligereza. Microservicios de red separados quedan condicionados a resultados de carga y necesidades operativas.
- ADR-002: selección y versión del proveedor EF Core compatible con .NET 10 y MySQL 8.4 requiere pruebas de migración reales.
- ADR-003: estrategia Angular SSR/híbrida se decide tras medir páginas públicas y verificar alojamiento futuro.
- ADR-004: storage routing de tenant dedicado requiere diseño y pruebas de cutover, rollback y backups sin contaminación.
- ADR-005: titulares jurídicos/copyright/emisores y formularios legales no se inventan y requieren evidencia documental.
- ADR-006: modalidades de cobro/PSP definitivos y cumplimiento legal quedan fuera de código local hasta integración autorizada.
- ADR-007: presupuestos de rendimiento y máximos por plan se fijan tras benchmarks sintéticos.
- ADR-008: protección de ramas y políticas de merge deben confirmarse realmente en GitHub; ver docs/REPOSITORY_GATES.md.
- ADR-009: subir la copia exacta del Documento Maestro Único al repositorio y validar su SHA; pendiente.

Las decisiones abiertas no bloquean leer el plan ni preparar la estructura, pero sí los cierres técnicos correspondientes. No cerrarlas por intuición.
