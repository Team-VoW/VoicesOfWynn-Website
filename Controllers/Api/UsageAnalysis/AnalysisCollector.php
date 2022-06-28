<?php

namespace VoicesOfWynn\Controllers\Api\UsageAnalysis;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\UsageAnalysis\BootupLogger;

class AnalysisCollector extends ApiController
{

    public function process(array $args): int
    {
        if (!isset($_REQUEST['apiKey'])) {
            return 401;
        }

        switch ($args[0]) {
            case 'ping':
                return $this->logBootup();
            /*
             * case 'read operation':
             *  //TODO
             *  break;
             */
            default:
                return 400;
        }
    }

    private function logBootup()
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::PING_API_KEY) {
            return 401;
        }
        $logger = new BootupLogger();
        return $logger->logBootup($_REQUEST['uuid'], $_SERVER['REMOTE_ADDR']);
    }
}
