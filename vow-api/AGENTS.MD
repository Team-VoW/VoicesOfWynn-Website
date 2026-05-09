.net 10 backend.
No Repositories should be injected into Controllers. Services should be used.
Keep a clean structure.
If a service grows to big it should be split up. Avoid 1k lines files.


podman compose -f docker-compose.dev.yml restart vow-api #This to simply restart the api service

Database structure is managed by Liquibase in the PHP app.