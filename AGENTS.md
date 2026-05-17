# Voices of Wynn Website

PHP website for the Voices of Wynn project — a Wynncraft mod that adds voiced NPC dialogue. Custom MVC framework under `website/`: routes defined in `website/routes.ini`, controllers in `website/Controllers/`, models in `website/Models/`, views as `.phtml` files in `website/Views/`.

## Guidelines

- Keep logic out of views. Data shaping and conditionals belong in controllers or models; views should only render pre-computed variables.
- Prefer model methods over `ContentManager`. `ContentManager` is a legacy convenience class for a narrow set of info-getting procedures. Logic that naturally belongs to a database model (`Quest`, `Npc`, etc.) should live there as a method/getter, not in `ContentManager`. Do not expand it for new features.
- Avoid static database fetchers on models; prefer constructing a model with identifying data and loading the rest through an instance method.
- One column-to-property mapping per model: the constructor's `$data` switch (or a `setData()` it delegates to). `loadFromX()` methods feed their fetched row into it instead of re-mapping.
- XSS escaping is handled globally by `WebpageController`. Do not call `htmlspecialchars()` manually in views.
- Check PDO query success by testing whether the result is not `false` (truthy check).
- Don't cast numeric strings to int unless strictly necessary — PHP's implicit conversion handles it.
- Validate IDs against the database, not with `is_numeric()` / `> 0` checks alone. Those pass values like `42069` that don't correspond to real records. Either validate against the actual list of IDs from the DB, or skip numeric validation and let the DB query return no results naturally.
- Validate string inputs against the DB column length (check the schema). Use `mb_strlen()` for multi-byte safety.
- Shared logic between models (`Quest`, `Npc`, etc.) belongs in `ContentModel`, the abstract base class both extend.


After making API source code changes in the vow-api project, do
cd vow-api
dotnet build src/VoW.Api/VoW.Api.csproj
cd ..
docker-compose -f docker-compose.dev.yml build vow-api
docker-compose -f docker-compose.dev.yml up -d vow-api

The dev service mounts the Debug DLL from the local build output, so source edits won't take effect in the container without `dotnet build`.

If `up -d vow-api` fails because an existing container name is already in use, run
docker-compose -f docker-compose.dev.yml up -d --force-recreate vow-api
