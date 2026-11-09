# Documento Maestro Único — referencia canónica CRECISOL

**Estado:** EDICIÓN INSTITUCIONAL VIGENTE para planificación, aprobada para versionamiento el 8 de noviembre de 2026. **No equivale a software implementado ni a certificación de seguridad.**

| Formato | Ruta canónica | Tamaño real | Git blob SHA-1 | SHA-256 del archivo |
| --- | --- | ---: | --- | --- |
| Word editable — autoridad | `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` | 1 344 481 bytes | `214bf9c3d9763be23a77b7e0c464359584edde46` | `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e` |
| PDF de lectura e impresión | `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf` | 4 490 865 bytes | `455847ae0cc3bf02b9b9c4c3435476d093918ea2` | `17ffe48aab3b224cc9b11927c498e4f49c162d96bf639c894b84259f597738dd` |

## Validación de procedencia y alcance

- Los archivos binarios subidos a `dev` (commit `a9c678e89a1e562c9c32c784ba331e1c59c014d7`) coinciden con los originales de la entrega final por **tamaño y SHA de blob Git**. Sus SHA-256 locales se consignan en la tabla.
- Word: 37 capítulos de producto y arquitectura; además, reglamento técnico y secciones transversales. Comprobación de estructura: 189 tablas y una imagen de marca incrustada.
- PDF: 229 páginas; portada con el logotipo oficial de CRECISOL, tabla de contenido y capítulos. El PDF es la representación del Word, **no un segundo documento maestro**.
- El plan anterior está absorbido como contenido histórico de referencia; no debe reintegrarse ni reemplazar esta edición. La autoridad documental reside únicamente en el Word señalado.
- La rama `main` deberá conservar **estos mismos blobs** tras integrar el PR documental de reconciliación; el control se realiza comparando los SHA Git, no por nombre o apariencia.

## Reglas de gobierno

- Usar esta edición para requisitos de producto, arquitectura, seguridad, parametrización, diseño e implementación secuencial de los **37 capítulos**.
- El alcance incluye MySQL compartido por defecto con aislamiento obligatorio, modalidad dedicada opcional, login contextual sin selector, SuperAdmin con acceso excepcional auditado, componentes UI y multimedia seguros, y seguimiento universal de expedientes.
- Trabajar un capítulo por vez. Ninguno se considera IMPLEMENTADO, PROBADO o CERTIFICADO por el solo hecho de figurar en este documento; el estado actual de software es **1/37 capítulos certificados (únicamente C01 desde el 9 de noviembre de 2026)**, con alcance, aprobación y evidencia CI en `docs/c01/C01_08_AUDITORIA_Y_ACTA.md`; C02–C37 continúan sin certificar.
- Las versiones de Angular, Node.js, .NET y MySQL y los proveedores concretos requieren compatibilidad comprobada mediante ejecución de pruebas.
- Está prohibida toda escritura sobre `solqaryn/Solqaryn`. No realizar VPS, DNS, cobros reales, producción ni despliegues durante la etapa actual.
- Si cambia el Word, se deberá generar de ese mismo Word el PDF, recalcular tamaños, SHA-256 y blobs Git, revisar los 37 capítulos y aprobar un PR en CRECISOL. **No conservar dos maestros divergentes.**
