-- One-time local setup. Run as a Postgres superuser, passing the app password:
--   psql -U postgres -v app_password="'your-password'" -f scripts/init-databases.sql
-- The password must match POSTGRES_PASSWORD in .env.

SELECT format('CREATE ROLE chessplatform_app LOGIN PASSWORD %L', :app_password)
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'chessplatform_app') \gexec

SELECT format('CREATE DATABASE %I OWNER chessplatform_app', db)
FROM unnest(ARRAY['chessplatform_identity', 'chessplatform_game', 'chessplatform_puzzles',
                  'chessplatform_rating', 'chessplatform_tournaments']) AS db
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = db) \gexec
