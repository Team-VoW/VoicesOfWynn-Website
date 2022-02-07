<?php


namespace VoicesOfWynn\Controllers;


class Error403 extends Controller
{
    
    /**
     * Method setting the view and headers for the error page
     * @param array $args Leave this array empty - no data are used
     * @return bool TRUE, if the data was set successfully
     */
    public function process(array $args): bool
    {
        header("HTTP/1.1 403 Forbidden");
        
        self::$data['error403_title'] = 'Access Denied';
        self::$data['error403_description'] = 'Oops, you probably didn\'t want to end up here.';
        self::$data['error403_keywords'] = '';
        
        self::$views = array('error403'); //Remove header, footer and stuff
		self::$cssFiles = array('errors');
        return true;
    }
}

