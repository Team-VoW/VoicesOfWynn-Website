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

## Bootup Actions Endpoints

| Error Code | Description |
|------------|-------------|
| `MISSING_REQUIRED_PARAMETER` | Required parameter 'id' is missing from the request |

## Line Reporting Endpoints

| Error Code | Description |
|------------|-------------|
| `MISSING_STATUS` | The 'status' parameter is required but was not provided |
| `NO_LINES_PROVIDED` | No lines were provided in the request, or the lines array is empty |

## Content Endpoints

| Error Code | Description |
|------------|-------------|
| `MISSING_QUEST_ID` | The 'questId' parameter is required but was not provided |
| `INVALID_QUEST_ID` | The 'questId' parameter must be a numeric value |

## Discord Integration Endpoints

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported by this endpoint |

## Usage Analysis Endpoints

| Error Code | Description |
|------------|-------------|
| `UNKNOWN_ACTION` | The requested action is not recognized or supported by this endpoint |

## Notes

- Error codes 401 (Unauthorized), 404 (Not Found), and 405 (Method Not Allowed) do not include a response body
- All other error responses should use the standardized JSON format shown above
