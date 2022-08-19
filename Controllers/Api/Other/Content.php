<?php

namespace VoicesOfWynn\Controllers\Api\Other;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Website\ContentManager;

class Content extends ApiController 
{
    public function process(array $args): int
    {
        switch ($args[0]) {
            case 'quests':
                return $this->getQuest();
            default:
                return 400;
        }
    }
    private function getQuest(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }

        if($_GET['questId'] === null || $_GET['questId'] === ''){
            return 404;
        }
        
        $cnm = new ContentManager();
		$questId = $_GET['questId'];
		if (!is_numeric($questId)) {
			return 406;
		}
        $quest = $cnm->getQuests($questId);
        echo json_encode($quest);
        return 200;
    }
}