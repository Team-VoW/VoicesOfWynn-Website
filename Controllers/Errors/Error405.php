<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error405 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error405_title'] = 'Invalid request';
        self::$data['error405_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error405_keywords'] = '';

        self::$view = 'error405';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 405 Method not Allowed");
    }
}

