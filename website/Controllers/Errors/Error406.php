<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error406 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error406_title'] = 'Invalid request';
        self::$data['error406_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error406_keywords'] = '';

        self::$view = 'error406';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 406 Not Acceptable");
    }
}

