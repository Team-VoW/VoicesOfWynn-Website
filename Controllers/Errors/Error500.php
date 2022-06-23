<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error500 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error500_title'] = 'Error';
        self::$data['error500_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error500_keywords'] = '';

        self::$view = 'error500';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 500 Internal Server Error");
    }
}

