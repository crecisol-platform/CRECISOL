-- ONLY disposable MySQL 8.4 CI database: crecisol_c01_test.
-- Uses synthetic IDs, no credentials, no customer data.
INSERT INTO brand_scopes(scope_kind, scope_id, created_at_utc) VALUES
  ('PLATFORM', X'00000000000000000000000000000000', UTC_TIMESTAMP(6)),
  ('TENANT', X'11111111111111111111111111111111', UTC_TIMESTAMP(6)),
  ('TENANT', X'22222222222222222222222222222222', UTC_TIMESTAMP(6));

INSERT INTO brand_revisions(
  scope_kind, scope_id, revision_number, name, slogan, descriptor,
  primary_color, accent_color, logo_object_key, website_url, created_at_utc)
VALUES
  ('PLATFORM', X'00000000000000000000000000000000', 1,
   'CRECISOL', 'Tu negocio, a tu manera.',
   'Comercio · Servicios · Membresías · Crecimiento',
   '#062E65', '#078FE8', 'crecisol-logo-oficial', NULL, UTC_TIMESTAMP(6)),
  ('TENANT', X'11111111111111111111111111111111', 1,
   'Comercio de prueba', 'Ejemplo seguro', 'Negocio ficticio',
   '#113355', '#1177AA', 'logo-demostracion', NULL, UTC_TIMESTAMP(6));

INSERT INTO brand_publications(scope_kind, scope_id, revision_number, published_at_utc)
VALUES
  ('PLATFORM', X'00000000000000000000000000000000', 1, UTC_TIMESTAMP(6)),
  ('TENANT', X'11111111111111111111111111111111', 1, UTC_TIMESTAMP(6));
