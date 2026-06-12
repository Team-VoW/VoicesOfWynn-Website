<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error401 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error401_title'] = 'Login Required';
        self::$data['error401_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error401_keywords'] = '';

        self::$view = 'error401';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 401 Unauthorized");
    }
}

