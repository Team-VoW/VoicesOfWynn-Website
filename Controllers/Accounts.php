<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\AccountManager;
use VoicesOfWynn\Models\User;

class Accounts extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
    	if (count($args) > 0)
    	{
    		switch (array_shift($args)) {
			    case 'clear-bio':
			    	$user = new User();
			    	$user->setData(array('id' => $args[0]));
			    	$user->clearBio();
				    header('Location: /administration/accounts');
				    exit();
			    case 'clear-avatar':
				    $user = new User();
				    $user->setData(array('id' => $args[0]));
			    	$user->clearAvatar();
			    	header('Location: /administration/accounts');
			    	exit();
			    default:
				    $errorController = new Error404();
				    $errorController->process(array());
		    }
	    }
    		
        self::$data['base_description'] = 'Tool for the administrators to manage accounts of the contributors.';
    
        $accountManager = new AccountManager();
        self::$data['accounts_roles'] = $accountManager->getRoles();
        self::$data['accounts_accounts'] = $accountManager->getUsers();
    
        self::$views[] = 'accounts';
        
        return true;
    }
}

