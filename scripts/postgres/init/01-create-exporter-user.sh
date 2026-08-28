#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
       IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'postgres_exporter') THEN
          CREATE ROLE postgres_exporter WITH LOGIN PASSWORD '$POSTGRES_EXPORTER_PASSWORD';
       END IF;
    END
    \$\$;

    GRANT pg_monitor TO postgres_exporter;
    GRANT CONNECT ON DATABASE $POSTGRES_DB TO postgres_exporter;
EOSQL
