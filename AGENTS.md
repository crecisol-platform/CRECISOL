# Agentes y colaboradores — CRECISOL

1. Alcance permitido: **solqaryn-platform/CRECISOL** exclusivamente. Nunca escribir en **solqaryn/Solqaryn** ni en su Skill.
2. Cargar primero .agents/skills/crecisol-project-governance/SKILL.md y los documentos que indique; atender la referencia al Documento Maestro Único en docs/MASTER_REFERENCE.md.
3. Verificar repositorio, rama, diff actual y permisos antes de editar. Si main carece de protección, priorizar PR en dev y no asumir protección.
4. No desplegar a VPS, dominios, Cloudflare, servicios externos ni configurar cobros en la etapa de código local.
5. Avance **secuencial C01 → C37**: el capítulo N solo inicia después de certificar N-1. **C01 y C02 están certificados (2/37); C03-01, C03-02 y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE y ACEPTADA EXPRESAMENTE PARA CERTIFICACIÓN (4/10, condicionada a su propia CI documental PREMERGE/POSTMERGE); C03-05 NO INICIADA, C03 NO CERTIFICADO y C04 BLOQUEADO**. Ningún capítulo futuro se declara implementado por figurar en el plan. Cada certificación requiere compilación/pruebas reales, auditorías A/B/C, acta y aprobación humana expresa.
6. Aislamiento de datos por tenant, sucursal y propiedad de recurso; RBAC/ABAC en backend, no solo en frontend. Registro de auditoría propio de cada tenant, sin acceso horizontal. Admin global solo con flujos excepcionales auditados.
7. No usar credenciales, secretos, datos reales ni endpoints de SOLQARYN como atajo. Nunca incluir .env ni conexiones reales en commits.
8. Alinear frontend/, backend/ y database/ con arquitectura limpia. Versiones del maestro son referencia hasta comprobar instalación, compatibilidad y pruebas reales.
9. Proteger valores editables: no permitir código/SQL/HTML/JS arbitrario desde configuración.
10. Cierre de cada PR: objetivo, diff, pruebas reales, negativas de seguridad, riesgos, rollback, documentación y estado verificable. Cero afirmaciones de certificación sin evidencia.
