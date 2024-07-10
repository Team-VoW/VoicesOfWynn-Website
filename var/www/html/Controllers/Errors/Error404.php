<?php


namespace VoicesOfWynn\Controllers\Errors;

use VoicesOfWynn\Controllers\Errors\ErrorController;

class Error404 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error404_title'] = 'Page not Found';
        self::$data['error404_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error404_keywords'] = '';

        self::$view = 'error404';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 404 Not Found");
    }
}

