# Agentes y colaboradores — CRECISOL

1. Alcance permitido: **solqaryn-platform/CRECISOL** exclusivamente. Nunca escribir en **solqaryn/Solqaryn** ni en su Skill.
2. Cargar primero .agents/skills/crecisol-project-governance/SKILL.md y los documentos que indique; atender la referencia al Documento Maestro Único en docs/MASTER_REFERENCE.md.
3. Verificar repositorio, rama, diff actual y permisos antes de editar. Si main carece de protección, priorizar PR en dev y no asumir protección.
4. No desplegar a VPS, dominios, Cloudflare, servicios externos ni configurar cobros en la etapa de código local.
5. Avance **secuencial C01 → C37**: C01 y C02 CERTIFICADOS (2/37); **C03-01, C03-02, C03-03, C03-04 Y C03-05 CERTIFICADAS (5/10)**, PR #58 y **11/11 POSTMERGE SUCCESS** sobre SHA 12fb19b0c35038c297939e17b27770c4fd63f77e ([evidencia](docs/c03/C03_05_CERTIFICACION_POSTMERGE.md)); **C03-06 NO INICIADA, C03 NO CERTIFICADO, C04 BLOQUEADO**. Históricos: C03-04 CERRADA TÉCNICAMENTE (4/10) antes de PR #56; C03-05 CERRADA TÉCNICAMENTE (5/10) antes del PR #58. Toda certificación exige pruebas reales, auditorías A/B/C y acta, no solo documentación.
6. Aislamiento de datos por tenant, sucursal y propiedad de recurso; RBAC/ABAC en backend, no solo en frontend. Registro de auditoría propio de cada tenant, sin acceso horizontal. Admin global solo con flujos excepcionales auditados.
7. No usar credenciales, secretos, datos reales ni endpoints de SOLQARYN como atajo. Nunca incluir .env ni conexiones reales en commits.
8. Alinear frontend/, backend/ y database/ con arquitectura limpia. Versiones del maestro son referencia hasta comprobar instalación, compatibilidad y pruebas reales.
9. Proteger valores editables: no permitir código/SQL/HTML/JS arbitrario desde configuración.
10. Cierre de cada PR: objetivo, diff, pruebas reales, negativas de seguridad, riesgos, rollback, documentación y estado verificable. Cero afirmaciones de certificación sin evidencia.
