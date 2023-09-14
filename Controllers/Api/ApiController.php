<?php

namespace VoicesOfWynn\Controllers\Api;

use VoicesOfWynn\Controllers\Controller;

/**
 * Base class for all API controllers
 */
abstract class ApiController extends Controller
{

    /* All API keys go here */
    //Line reporting keys
    const REPORTING_API_KEY = 'testing';
    const COLLECTING_API_KEY = 'testing';
    const UPDATING_API_KEY = 'testing';
    //Usage analysis api keys
    const AGGREGATE_API_KEY = 'testing';
    //Discord integration key
    const DISCORD_INTEGRATION_API_KEY = 'testing';
    //Premium authenticator key
    const PREMIUM_AUTHENTICATOR_API_KEY = 'testing';

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
}