<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\AccountManager;
use VoicesOfWynn\Models\User;
use VoicesOfWynn\Models\UserException;

class NewAccount extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
	        default:
	        	return false;
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
            $password = $user->register($_POST['name'], true);
            self::$data['newaccount_password'] = $password;
        } catch (UserException $e) {
            self::$data['newaccount_error'] = $e->getMessage();
        }
        return $result;
    }
}

