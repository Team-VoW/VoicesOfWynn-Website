<?php

namespace VoicesOfWynn\Controllers\Api;

use VoicesOfWynn\Controllers\Controller;
use VoicesOfWynn\Controllers\Api\ApiKey;
use OpenApi\Attributes as OA;

#[OA\Info(
    title: "Voices of Wynn API",
    version: "1.0.0",
    description: "API for the Voices of Wynn website and mod."
)]

#[OA\Schema(
    schema: "Error",
    description: "Standard error response",
    properties: [
        new OA\Property(property: "error", type: "string", description: "Error message"),
        new OA\Property(property: "code", type: "integer", description: "HTTP status code")
    ]
)]
abstract class ApiController extends Controller
{


    /**
     * Controller constructor enabling output buffering and setting the Content-Type header
     * Since specific controllers don't have a constructor, this will be invoked every time a new constructor is
     * instantiated
     */
    public function __construct()
    {
        header('Content-Type: application/json');

        //Start output buffering and keep it enabled for the whole duration of processing the request.
        //Output is harvested and returned in the getOutput() method
        ob_start();
    }

    /**
     * @inheritDoc
     */
    public abstract function process(array $args): int;

    /**
     * Method returning the final response of this API request
     * This is called from the RouterController
     * @return string Final response to send to the client
     */
    public function getOutput() {
        $result = ob_get_contents();
        ob_end_clean();
        return $result;
    }

    /**
     * Method checking the validity of an API key defined in ApiKeys.ini
     * @param ApiKey $keyType Type of the API key being validated
     * @param string $key Key provided by the client
     * @return bool TRUE if the key is valid, FALSE if it's invalid or the key type doesn't exist
     */
    protected function checkApiKey(ApiKey $keyType, string $key): bool
    {
        $keys = parse_ini_file('ApiKeys.ini');
        return (
            in_array(strtolower($keyType->name), array_keys($keys))
            &&
            $keys[strtolower(($keyType->name))] === $key
        );
    }

    /**
     * Sends a standardized 400 Bad Request error response with error code and message
     * @param string $errorCode The error code identifier (see API_ERROR_CODES.md)
     * @param string $message Human-readable explanation of the error
     * @return int Always returns 400
     */
    protected function sendBadRequestError(string $errorCode, string $message): int
    {
        echo json_encode([
            'error_code' => $errorCode,
            'message' => $message
        ]);
        return 400;
    }
}

