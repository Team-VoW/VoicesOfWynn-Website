<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error400 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error400_title'] = 'Error';
        self::$data['error400_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error400_keywords'] = '';

        self::$view = 'error400';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 400 Bad Request");
    }
}

