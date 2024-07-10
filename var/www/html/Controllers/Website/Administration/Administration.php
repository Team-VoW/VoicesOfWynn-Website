<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\Npc;
use VoicesOfWynn\Controllers\Website\WebpageController;

class Administration extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if (!isset($_SESSION['user'])) {
            //No user is logged in
            return 401;
        }
        if (!$_SESSION['user']->isSysAdmin()) {
            //The logged user is not system admin
            return 403;
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
            case 'mass-upload':
                $nextController = new Upload();
                break;
            case 'new-release':
                $nextController = new NewRelease();
                break;
	        default:
	        	$nextController = new Accounts();
        }

        return $nextController->process($args);
    }
}

