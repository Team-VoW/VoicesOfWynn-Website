<?php

namespace VoicesOfWynn\Controllers\Api\UsageAnalysis;

use DateTime;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\UsageAnalysis\BootupLogger;
use VoicesOfWynn\Models\Api\UsageAnalysis\PingAggregator;
use OpenApi\Attributes as OA;

#[OA\Tag(name: "Usage Analysis", description: "Endpoints for usage analysis.")]
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
                return $this->sendBadRequestError('UNKNOWN_ACTION', 'The requested action is not recognized');
        }
    }

    #[OA\Put(
        path: "/api/usage-analysis/aggregate",
        summary: "Aggregate usage data",
        tags: ["Usage Analysis"]
    )]
    #[OA\RequestBody(
        required: true,
        content: new OA\MediaType(
            mediaType: "application/x-www-form-urlencoded",
            schema: new OA\Schema(
                required: ["apiKey"],
                properties: [
                    new OA\Property(property: "apiKey", type: "string", default: "testing")
                ]
            )
        )
    )]
    #[OA\Response(response: 204, description: "Success")]
    #[OA\Response(response: 401, description: "Unauthorized")]
    #[OA\Response(response: 500, description: "Internal server error")]
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
