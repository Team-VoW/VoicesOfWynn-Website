<?php


namespace VoicesOfWynn\Controllers\Errors;

use VoicesOfWynn\Controllers\Errors\ErrorController;

class Error403 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error403_title'] = 'Access Denied';
        self::$data['error403_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error403_keywords'] = '';

        self::$view = 'error403';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 403 Forbidden");
    }
}

