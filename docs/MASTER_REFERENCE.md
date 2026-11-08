# Documento Maestro Único — fuente canónica CRECISOL

**Estado documental:** cargado y verificado en GitHub (2026-11-08). **No implica software implementado ni seguridad certificada.**

| Formato | Ruta | Tamaño | Git blob SHA-1 | SHA-256 del archivo original |
| --- | --- | ---: | --- | --- |
| Word editable, autoridad | `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` | 262754 bytes | `33148ae1b4c3a1c89efba7cae9fea73a8ff2fb2a` | `a43a0ea0c7988cd52dc6a7912b63a3055abdff2188062014e69caa19c69e3c71` |
| PDF de lectura / impresión | `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf` | 3396652 bytes | `b56d038dc891ad4cdafd26884704190c8b7069b0` | `305c36e803310f0ab473d80ae2b2cf825bec7d5944d2c9037836a53c526a1992` |

## Evidencia de integridad
- Se calcularon SHA-256 y blob hash Git sobre los dos archivos originales disponibles en el entorno de trabajo.
- Se compararon los blob hashes y tamaños con los objetos reales almacenados en `solqaryn-platform/CRECISOL` en las ramas `dev` y `main`. Los dos coincidieron exactamente.
- SHA-256 acredita los originales locales; la coincidencia exacta del Git blob enlaza esos originales con el contenido subido. No se infiere el SHA-256 de un mero nombre de archivo.
- Para futuras modificaciones, recalcular y registrar ambos hashes en el mismo PR; no generar otra autoridad maestra.

## Alcance
Este es el único Documento Maestro Único de CRECISOL, de 37 capítulos, con sus ampliaciones transversales de aislamiento de tenants (shared y dedicated), identidades y accesos, login contextual sin selector, SuperAdmin con acceso auditado, parametrización, diseño visual, previsualizaciones y seguimiento universal de pedidos/citas/expedientes.

**Regla de trabajo:** consultar el documento por capítulo; desarrollar C01 antes de C02, y así sucesivamente hasta C37. Tres auditorías, pruebas reales y aceptación para declarar un capítulo CERTIFICADO. Este documento no sustituye pruebas de Angular/.NET/MySQL ni una certificación.

**Proyecto exclusivamente CRECISOL:** prohibidas las escrituras a `solqaryn/Solqaryn`; etapa actual solo desarrollo local, código, documentación y pruebas; sin despliegues ni producción.
