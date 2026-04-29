<?php

namespace VoicesOfWynn\Controllers\Website\Administration;

use VoicesOfWynn\Controllers\Website\Npc;
use VoicesOfWynn\Controllers\Website\WebpageController;

class Administration extends WebpageController
{
    private ?WebpageController $nextController = null;

    public function displayView(): string
    {
        return $this->nextController?->displayView() ?? parent::displayView();
    }
    
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
        
        switch (array_shift($args)) {
            case 'accounts':
                $this->nextController = new Accounts();
                break;
            case 'new-account':
                $this->nextController = new NewAccount();
                break;
            case 'new-quest':
                $this->nextController = new NewQuest();
                break;
            case 'new-npc':
                $this->nextController = new NewNpc();
                break;
            case 'npcs':
                $this->nextController = new Npcs();
                break;
	        case 'npc':
	        	$this->nextController = new Npc();
	        	break;
            case 'mass-upload':
                $this->nextController = new Upload();
                break;
	        default:
	        	$this->nextController = new Accounts();
        }

        return $this->nextController->process($args);
    }
}
