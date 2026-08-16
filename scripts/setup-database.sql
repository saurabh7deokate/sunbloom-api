-- SunBloom local development database.
--
-- Creates a dedicated role and database on PostgreSQL 18. Run against the PG18
-- server (port 5433), NOT the PostgreSQL 12 instance on the default port 5432 —
-- PG12 reached end of life in November 2024 and holds unrelated databases.
--
-- Usage (you will be prompted for the postgres superuser password):
--
--   psql -U postgres -h localhost -p 5433 \
--        -v app_password="'choose-a-strong-password'" \
--        -f scripts/setup-database.sql
--
-- Note the nested quotes in -v: psql substitutes :app_password literally, so the
-- value must carry its own single quotes.

\set ON_ERROR_STOP on

-- Application role. The API never connects as a superuser.
CREATE ROLE sunbloom WITH LOGIN PASSWORD :app_password;

COMMENT ON ROLE sunbloom IS 'SunBloom application role - least privilege, not a superuser';

CREATE DATABASE sunbloom_dev
    OWNER      sunbloom
    ENCODING   'UTF8'
    TEMPLATE   template0;

\connect sunbloom_dev

-- Case-insensitive email uniqueness is enforced at the column level (identity.users.email).
CREATE EXTENSION IF NOT EXISTS citext;

-- The public schema is not used; every module owns a named schema (ADR-0006).
REVOKE ALL ON SCHEMA public FROM PUBLIC;

CREATE SCHEMA IF NOT EXISTS identity   AUTHORIZATION sunbloom;
CREATE SCHEMA IF NOT EXISTS catalog    AUTHORIZATION sunbloom;
CREATE SCHEMA IF NOT EXISTS competency AUTHORIZATION sunbloom;
CREATE SCHEMA IF NOT EXISTS shared     AUTHORIZATION sunbloom;

\echo ''
\echo 'Created database sunbloom_dev with schemas: identity, catalog, competency, shared'
\echo ''
\echo 'Next, store the connection string in user-secrets (never in appsettings.json):'
\echo ''
\echo '  dotnet user-secrets set "ConnectionStrings:SunBloomDb" \'
\echo '    "Host=localhost;Port=5433;Database=sunbloom_dev;Username=sunbloom;Password=<the password you chose>" \'
\echo '    --project src/SunBloom.Api'
\echo ''
