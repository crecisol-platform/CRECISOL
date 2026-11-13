# Agentes y colaboradores — CRECISOL

**ESTADO OPERATIVO VIGENTE (12-11-2026):** C01, C02 y C03 están certificados en sus alcances documentados; C03 está cerrado en 10/10 microfases y el total institucional es 3/37 capítulos. C04 está en desarrollo: C04-01 quedó certificado solo como contratos internos, C04-02..08 tienen estados técnicos condicionados y C04-09 se prepara en una rama separada, todavía no integrada ni certificada. Nada de producción ni SOLQARYN.

**UBICACIÓN OPERATIVA VIGENTE:** repositorio `crecisol-platform/CRECISOL`, rama de integración `dev`; [registro de migración](docs/MIGRACION_ORGANIZACION_GITHUB_2026_11_11.md). Las referencias a PR #79 y a estados anteriores se conservan únicamente como historial y no prevalecen sobre el registro vigente. No escribir a SOLQARYN.

**AUTORIDAD DEL ESTADO:** consultar [docs/ESTADO_VIGENTE_CAPITULOS.md](docs/ESTADO_VIGENTE_CAPITULOS.md) y el registro de C03. Las actas y párrafos anteriores que describen C03 pendiente o C04 bloqueado son capturas históricas; no deben usarse para determinar el estado actual. Sin desplegar ni ejecutar Cloudflare, DNS, pagos o SOLQARYN.

**ACTUALIZACIÓN POSTERIOR DEL PROPIETARIO (11-11-2026, C03-10):** CRECISOL ahora es público y se autoriza el intento de restituir GitHub Actions automáticos para auditar C03-10. Aplicar [decisión prospectiva CI](docs/DECISION_OPERATIVA_2026-11-11_RETORNO_CI_C03.md) antes de la decisión previa que no dependía de Actions. A/B/C y evidencia real siguen siendo obligatorias; nada autoriza despliegues, producción, pagos o escrituras a SOLQARYN.

**REFERENCIA HISTÓRICA PARA REGRESIONES (NO ESTADO ACTUAL):** las actas de C03-08 y anteriores conservan sus fotografías originales. El estado operativo actual es C03 certificado 10/10; C04 está en desarrollo y C04-09 permanece en preparación separada.

**DECISIÓN OPERATIVA DEL PROPIETARIO (11-11-2026):** Gobierno autónomo SIN dependencia de GitHub Actions y SIN PC del propietario. Leer antes de actuar [decisión sin Actions](docs/DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md). GitHub se usa exclusivamente para alojar y versionar código/documentos. El asistente programa, revisa, audita, aprueba y decide certificaciones con evidencia real suficiente y tres auditorías A/B/C más estrictas. **No exigir CI PREMERGE/POSTMERGE, PR, un runner GitHub ni firma humana nueva como gate**. Este cambio es prospectivo: no reescribir actas ni afirmar pruebas no realizadas, no saltarse P0/P1 ni certificar runtime imposible de verificar. Ningún trabajo asíncrono ni despliegue queda autorizado.


1. Alcance permitido: **crecisol-platform/CRECISOL** exclusivamente. Nunca escribir en **solqaryn/Solqaryn** ni en su Skill.
2. Cargar primero .agents/skills/crecisol-project-governance/SKILL.md y los documentos que indique; atender la referencia al Documento Maestro Único en docs/MASTER_REFERENCE.md.
3. Verificar repositorio, rama, diff actual y permisos antes de editar. Si main carece de protección, priorizar PR en dev y no asumir protección.
4. No desplegar a VPS, dominios, Cloudflare, servicios externos ni configurar cobros en la etapa de código local.
5. Avance **secuencial C01 → C37**: C01, C02 y C03 están certificados en sus alcances documentados (3/37); C03 está cerrado en 10/10 microfases. C04 está en desarrollo: C04-01 certificado solo en contratos internos, C04-02..08 condicionados por sus puertas documentales/técnicas y C04-09 en preparación en una rama separada, sin integración a `dev` ni certificación. Histórico trazable sin cambiar actas: ninguna certificación anterior se revoca ni reescribe. C03-07 abarca solo outbox sintético PLATFORM; no implica autorización privada, tenant routing, consumidores productivos, pagos ni despliegues. A/B/C, pruebas reales suficientes, acta, revisión de seguridad y rollback siguen siendo obligatorios; no hay producción ni SOLQARYN.
6. Aislamiento de datos por tenant, sucursal y propiedad de recurso; RBAC/ABAC en backend, no solo en frontend. Registro de auditoría propio de cada tenant, sin acceso horizontal. Admin global solo con flujos excepcionales auditados.
7. No usar credenciales, secretos, datos reales ni endpoints de SOLQARYN como atajo. Nunca incluir .env ni conexiones reales en commits.
8. Alinear frontend/, backend/ y database/ con arquitectura limpia. Versiones del maestro son referencia hasta comprobar instalación, compatibilidad y pruebas reales.
9. Proteger valores editables: no permitir código/SQL/HTML/JS arbitrario desde configuración.
10. Cierre de cada microfase o integración Git, con PR opcional: objetivo, SHA/diff, pruebas reales, negativas de seguridad, riesgos, rollback, documentación y estado verificable. Cero afirmaciones de certificación sin evidencia.
