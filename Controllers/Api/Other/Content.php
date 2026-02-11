<?php

namespace VoicesOfWynn\Controllers\Api\Other;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiErrorCode;
use VoicesOfWynn\Models\Website\ContentManager;

class Content extends ApiController 
{
    public function process(array $args): int
    {
        switch ($args[0]) {
            case 'quests':
                return $this->getQuest();
            default:
                return $this->sendBadRequestError(ApiErrorCode::UNKNOWN_ACTION, 'The requested action is not recognized');
        }
    }
    private function getQuest(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }

        if(!isset($_GET['questId']) || $_GET['questId'] === null || $_GET['questId'] === ''){
            return $this->sendBadRequestError(ApiErrorCode::MISSING_QUEST_ID, 'The \'questId\' parameter is required');
        }

        $questId = $_GET['questId'];
        if (!is_numeric($questId)) {
            return $this->sendBadRequestError(ApiErrorCode::INVALID_QUEST_ID, 'The \'questId\' parameter must be a numeric value');
        }

        try {
            $cnm = new ContentManager();
            $quest = $cnm->getQuests($questId);
            echo json_encode($quest);
            return 200;
        } catch (\Exception $e) {
            error_log('Content::getQuest error: ' . $e->getMessage());
            return 500;
        }
    }
}