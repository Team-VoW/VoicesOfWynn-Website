<?php


namespace VoicesOfWynn\Controllers;


abstract class Controller
{
    
    /**
     * @var $data array Data obtained by all controllers in the process
     */
    protected static $data;
    
    /**
     * @var $views array List of views to use, from the most outer one to the most inner one
     */
    protected static $views;
    
    /**
     * Public method processing passed data, specific for each controller
     * @param array $args Arguments to process
     * @return bool TRUE, if everything worked as expected, FALSE otherwise
     */
    public abstract function process(array $args): bool;
}
