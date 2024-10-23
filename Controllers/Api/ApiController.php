<?php

namespace VoicesOfWynn\Controllers\Api;

use VoicesOfWynn\Controllers\Controller;
use VoicesOfWynn\Models\Api\ApiKey\ApiKey;

/**
 * Base class for all API controllers
 */
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
}

