<?php

namespace VoicesOfWynn\Controllers\Errors;

class Error402 extends ErrorController
{

    protected function displayErrorWebsite()
    {
        self::$data['error402_title'] = 'Subscription Required';
        self::$data['error402_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error402_keywords'] = '';

        self::$view = 'error402';
    }

    protected function sendHttpErrorHeader()
    {
        header("HTTP/1.1 402 Payment Required");
    }
}

