<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\AccountManager;
use VoicesOfWynn\Models\User;

class Accounts extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
    	if (count($args) > 0)
    	{
    		switch (array_shift($args)) {
			    case 'clear-bio':
			    	$user = new User();
			    	$user->setData(array('id' => $args[0]));
			    	$user->clearBio();
				    return 204;
			    case 'clear-avatar':
				    $user = new User();
				    $user->setData(array('id' => $args[0]));
			    	$user->clearAvatar();
			    	return 204;
			    case 'delete':
				    $user = new User();
				    $user->setData(array('id' => $args[0]));
				    $user->delete();
				    return 204;
			    case 'grant-role':
				    $user = new User();
				    $user->setData(array('id' => $args[0]));
				    $user->addRole($args[1]);
					return 204;
			    case 'revoke-role':
				    $user = new User();
				    $user->setData(array('id' => $args[0]));
				    $user->removeRole($args[1]);
					return 204;
			    default:
				    return 400;
		    }
	    }

        self::$data['base_description'] = 'Tool for the administrators to manage accounts of the contributors.';
    
        $accountManager = new AccountManager();
        self::$data['accounts_roles'] = $accountManager->getRoles();
        self::$data['accounts_accounts'] = $accountManager->getUsers();
    
        self::$cssFiles[] = 'accounts';
        self::$jsFiles[] = 'accounts';
        self::$views[] = 'accounts';
        
        return true;
    }
}

