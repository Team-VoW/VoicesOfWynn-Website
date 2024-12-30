<?php

namespace VoicesOfWynn\Controllers\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\UsageAnalysis\BootupLogger;
use VoicesOfWynn\Models\Api\UsageAnalysis\PingAggregator;

class AnalysisProcessor extends ApiController
{

    public function process(array $args): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);
        
        if (!isset($_REQUEST['apiKey']) && !isset($_PUT['apiKey'])) {
            return 401;
        }

        switch ($args[0]) {
            case 'aggregate':
                return $this->aggregate();
            default:
                return 400;
        }
    }

    private function aggregate(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
            return 405;
        }
        parse_str(file_get_contents("php://input"),$_PUT);
        if (!$this->checkApiKey(ApiKey::STATISTICS_AGGREGATE, $_PUT['apiKey'])) {
            return 401;
        }
        $minDelay = max(BootupLogger::MINIMUM_DELAY_BETWEEN_PINGS_BY_IP, BootupLogger::MINIMUM_DELAY_BETWEEN_PINGS_BY_UUID);
        $minDelayDays = ceil($minDelay / 86400);
        $lastAggregatableDay = new DateTime();
        $minDelayDays++;
        $lastAggregatableDay->modify("-$minDelayDays days");

        $logger = new PingAggregator();
        return $logger->aggregateUpToDate($lastAggregatableDay);
    }
}
