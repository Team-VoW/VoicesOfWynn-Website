<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\AccountManager;

class Accounts extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        self::$data['base_description'] = 'Tool for the administrators to manage accounts of the contributors.';
    
        $accountManager = new AccountManager();
        self::$data['accounts_roles'] = $accountManager->getRoles();
        self::$data['accounts_accounts'] = $accountManager->getUsers();
    
        self::$views[] = 'accounts';
        
        return true;
    }
}

