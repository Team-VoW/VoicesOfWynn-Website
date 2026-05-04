# Voices of Wynn Website

PHP website for the Voices of Wynn project — a Wynncraft mod that adds voiced NPC dialogue. Custom MVC framework: routes defined in `routes.ini`, controllers in `Controllers/`, models in `Models/`, views as `.phtml` files in `Views/`.

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

## Admin/API notes

- Admin AJAX/JSON endpoints belong in `Controllers/Api/...`, not `Controllers/Website/...`; webpage controllers should render pages.
- Let the front controller set HTTP status codes from controller return values. Avoid `http_response_code()`, manual JSON headers, and `exit()` in controllers unless an existing framework pattern requires it.
- Collection/search/list queries should not be instance methods on `Npc`, `Quest`, etc. Use a dedicated query/service model instead.
- `loadFromId()` should use the ID already set on the model instance: construct with `['id' => $id]`, then call `loadFromId()`.
- For relation updates like adding/removing an NPC from a quest, prefer `PUT` unless the relation itself is modeled as a deletable resource.
- In JS, prefer stable class/data selectors over traversing text nodes or relying on heading structure.
