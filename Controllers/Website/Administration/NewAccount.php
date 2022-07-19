<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\UserException;

class NewAccount extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
	        default:
	        	return 405;
        }
    }
    
    public function get(array $args): bool
    {
        self::$data['base_description'] = 'Tool for the administrators to create new accounts for new contributors.';
        
        self::$data['newaccount_password'] = '';
        self::$data['newaccount_error'] = '';
        
        self::$views[] = 'new-account';
        return true;
    }
    
    public function post(array $args): bool
    {
        $result = $this->get(array());
        
        $user = new User();
        try {
            $password = $user->register($_POST['name'], $_POST['discord'], true);
            self::$data['newaccount_password'] = $password;
        } catch (UserException $e) {
            self::$data['newaccount_error'] = $e->getMessage();
        }
        return $result;
    }
}

