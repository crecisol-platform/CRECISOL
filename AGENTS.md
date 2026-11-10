# Agentes y colaboradores — CRECISOL

1. Alcance permitido: **solqaryn-platform/CRECISOL** exclusivamente. Nunca escribir en **solqaryn/Solqaryn** ni en su Skill.
2. Cargar primero .agents/skills/crecisol-project-governance/SKILL.md y los documentos que indique; atender la referencia al Documento Maestro Único en docs/MASTER_REFERENCE.md.
3. Verificar repositorio, rama, diff actual y permisos antes de editar. Si main carece de protección, priorizar PR en dev y no asumir protección.
4. No desplegar a VPS, dominios, Cloudflare, servicios externos ni configurar cobros en la etapa de código local.
5. Avance **secuencial C01 → C37**: 2/37 capítulos certificados (C01 y C02); C03-01 a C03-07 CERTIFICADAS (7/10), C03-05 CERTIFICADA (5/10), C03-06 CERTIFICADA (6/10), C03-07 CERTIFICADA (7/10) mediante PR #63 (8/8 CI POSTMERGE SUCCESS en a256885afa5ee2f639bd4ed6f7d8595b709e6409) y PR #64 (5/5 CI POSTMERGE SUCCESS en b1e019afabfb44f05076024b8d34cde74a340acc); C03-08 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO. Histórico: C03-05 certificada por PR #58, C03-06 por PR #61; ninguna certificación anterior se revoca ni reescribe. C03-07 abarca solo outbox sintético PLATFORM; no implica autorización privada, tenant routing, consumidores productivos, pagos ni despliegues. Toda certificación exige A/B/C, compilación .NET/MySQL real, acta, control de POSTMERGE y aceptación del propietario; C03 completo no se certifica por sumar microfases.
6. Aislamiento de datos por tenant, sucursal y propiedad de recurso; RBAC/ABAC en backend, no solo en frontend. Registro de auditoría propio de cada tenant, sin acceso horizontal. Admin global solo con flujos excepcionales auditados.
7. No usar credenciales, secretos, datos reales ni endpoints de SOLQARYN como atajo. Nunca incluir .env ni conexiones reales en commits.
8. Alinear frontend/, backend/ y database/ con arquitectura limpia. Versiones del maestro son referencia hasta comprobar instalación, compatibilidad y pruebas reales.
9. Proteger valores editables: no permitir código/SQL/HTML/JS arbitrario desde configuración.
10. Cierre de cada PR: objetivo, diff, pruebas reales, negativas de seguridad, riesgos, rollback, documentación y estado verificable. Cero afirmaciones de certificación sin evidencia.
