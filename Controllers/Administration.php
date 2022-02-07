<?php

namespace VoicesOfWynn\Controllers;

use VoicesOfWynn\Models\AccountManager;
use VoicesOfWynn\Models\Db;

class Administration extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        if (!isset($_SESSION['user']) || !$_SESSION['user']->isSysAdmin()) {
            //No user is logged in or the logged user is not system admin
            $errorController = new Error403();
            return $errorController->process(array());
        }
    
        self::$data['base_title'] = 'Administration';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Administration,Management';
        
        self::$views[] = 'administration';
        self::$cssFiles[] = 'administration';
        
        $nextController = null;
        switch (array_shift($args)) {
            case 'accounts':
                $nextController = new Accounts();
                break;
            case 'new-account':
                $nextController = new NewAccount();
                break;
            case 'npcs':
                $nextController = new Npcs();
                break;
	        case 'npc':
	        	$nextController = new Npc();
	        	break;
	        default:
	        	$nextController = new Accounts();
        }

        $result = $nextController->process($args);

        if ($result === false) {
            //The NPC that the user wants to manage doesn't exist
            return false;
        }

        return true;
    }
}

