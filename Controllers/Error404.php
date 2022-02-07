<?php


namespace VoicesOfWynn\Controllers;


class Error404 extends Controller
{
    
    /**
     * Method setting the view and headers for the error page
     * @param array $args Leave this array empty - no data are used
     * @return bool TRUE, if the data was set successfully
     */
    public function process(array $args): bool
    {
        header("HTTP/1.1 404 Not Found");
        
        self::$data['error404_title'] = 'Page not found';
        self::$data['error404_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error404_keywords'] = '';
        
        self::$views = array('error404'); //Remove header, footer and stuff
		self::$cssFiles = array('errors');
        return true;
    }
}

