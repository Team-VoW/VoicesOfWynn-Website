<?php


namespace VoicesOfWynn\Controllers;

/**
 * Base class for all controllers
 * Can be used as a direct class directly for controllers, that are performing some kind of operation and then redirect
 * to a different controller.
 * Parent class for WebpageController and ApiController
 */
abstract class Controller
{
    
    /**
     * Public method processing passed data, specific for each controller
     * @param array $args Arguments to process
     * @return int 1 (or TRUE), if everything worked as expected, HTTP error code otherwise
     */
    public abstract function process(array $args): int;
}

