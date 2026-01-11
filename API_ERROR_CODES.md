# API Error Codes

This document lists all error codes returned by the Voices of Wynn API for 400 Bad Request responses.

All 400 errors return a JSON response in the following format:
```json
{
  "error_code": "ERROR_CODE_HERE",
  "message": "Human-readable explanation of the error"
}
```

## General Error Codes

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported by this endpoint |
| `MISSING_REQUIRED_PARAMETER` | A required parameter is missing from the request |
| `INVALID_PARAMETER_TYPE` | A parameter has an invalid data type |
| `INVALID_PARAMETER_VALUE` | A parameter has an invalid value |
| `INVALID_PARAMETER_FORMAT` | A parameter format is incorrect |

## Bootup Actions Endpoints

### /api/version/check

| Error Code | Description |
|------------|-------------|
| `MISSING_REQUIRED_PARAMETER` | Required parameter 'id' is missing from the request |
| `INVALID_ID_FORMAT` | The 'id' parameter must be a valid SHA-256 hash (64 hexadecimal characters) |

## Line Reporting Endpoints

### /api/unvoiced-line-report/new

| Error Code | Description |
|------------|-------------|
| `MISSING_REQUIRED_PARAMETER` | Required parameter (full, npc, or player) is missing |
| `INVALID_PARAMETER_TYPE` | Coordinate parameters (x, y, z) must be integers |
| `INVALID_PARAMETER_LENGTH` | Parameter exceeds maximum length or is too short |
| `INVALID_COORDINATE_RANGE` | Coordinate values must be between -8388608 and 8388607 |

### /api/unvoiced-line-report/import

| Error Code | Description |
|------------|-------------|
| `MISSING_STATUS` | The 'status' parameter is required but was not provided |
| `INVALID_STATUS_VALUE` | Status must be one of: d, m, y, n, v |
| `MISSING_LINES_PARAMETER` | The 'lines' parameter is required |
| `INVALID_LINES_TYPE` | The 'lines' parameter must be an array |
| `EMPTY_LINES_ARRAY` | The 'lines' array cannot be empty |

### /api/unvoiced-line-report/index

| Error Code | Description |
|------------|-------------|
| `INVALID_NPC_NAME` | The 'npc' parameter contains invalid characters |

### /api/unvoiced-line-report/raw

| Error Code | Description |
|------------|-------------|
| `MISSING_LINE_PARAMETER` | The 'line' parameter is required |

### /api/unvoiced-line-report/resolve

| Error Code | Description |
|------------|-------------|
| `NO_LINES_PROVIDED` | No lines were provided or the lines array is empty |
| `INVALID_LINES_TYPE` | The 'lines' parameter must be an array |
| `MISSING_STATUS` | The 'status' parameter is required |
| `INVALID_STATUS_VALUE` | Status must be one of: r, d, m, y, n, v |

### /api/unvoiced-line-report/reset

| Error Code | Description |
|------------|-------------|
| `INVALID_NPC_NAME` | The 'npc' parameter contains invalid characters |

### /api/unvoiced-line-report/accepted, /active, /valid

| Error Code | Description |
|------------|-------------|
| `INVALID_MINREPORTS_TYPE` | The 'minreports' parameter must be a positive integer |
| `INVALID_DATE_FORMAT` | The 'youngerthan' parameter must be in Y-m-d format (e.g., 2025-01-15) |

## Content Endpoints

### /api/content/quest-info

| Error Code | Description |
|------------|-------------|
| `MISSING_QUEST_ID` | The 'questId' parameter is required but was not provided |
| `INVALID_QUEST_ID` | The 'questId' parameter must be a numeric value |

## Discord Integration Endpoints

### /api/discord-integration (GET)

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported |
| `MISSING_ACTION_PARAMETER` | The 'action' parameter is required |

### /api/discord-integration (POST)

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported |
| `MISSING_ACTION_PARAMETER` | The 'action' parameter is required |
| `MISSING_REQUIRED_PARAMETER` | Required parameter (discordId or discordName) is missing |
| `INVALID_DISCORD_ID` | The 'discordId' must be a numeric value |
| `INVALID_ROLES_JSON` | The 'roles' parameter must be valid JSON |

## Usage Analysis Endpoints

### /api/usage-analysis/aggregate

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported by this endpoint |

## Notes

- Error codes 401 (Unauthorized), 404 (Not Found), and 405 (Method Not Allowed) do not include a response body
- All other error responses should use the standardized JSON format shown above
- 500 Internal Server Error responses may include additional debug information in development environments
