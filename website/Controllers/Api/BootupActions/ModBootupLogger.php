<?php

namespace VoicesOfWynn\Controllers\Api\BootupActions;

use OpenApi\Attributes as OA;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\FunFacts\FunFactGenerator;
use VoicesOfWynn\Models\Api\MessageBroadcast\BroadcastLoader;
use VoicesOfWynn\Models\Api\UsageAnalysis\BootupLogger;
use VoicesOfWynn\Models\Api\VersionChecker\VersionChecker;

/**
 * Kept alive only for mod clients that are already in players' hands. New releases call
 * POST /vow-api/mod/bootup instead, which serves the same data - read from the same tables - in a
 * modern shape. This route goes away once those clients have been replaced.
 */
#[OA\Tag(name: "Bootup Actions", description: "Endpoints for mod bootup.")]
class ModBootupLogger extends ApiController
{

    #[OA\Get(
        path: "/api/version/check",
        summary: "Check for new version",
        tags: ["Bootup Actions"]
    )]
    #[OA\Parameter(name: "id", in: "query", required: true, schema: new OA\Schema(type: "string"))]
    #[OA\Response(
        response: 200,
        description: "Success"
    )]
    #[OA\Response(response: 400, description: "Bad request")]
    #[OA\Response(response: 500, description: "Internal server error")]
    public function process(array $args): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }

        $uuidHash = $_GET['id'] ?? null;
        if (empty($uuidHash)) {
            //No stats --> no fun fact, broadcast or version check for you
            return 400;
        }
        $ipHash = hash('sha256', $_SERVER['REMOTE_ADDR']);

        //Provide version info and fun fact
        $checker = new VersionChecker();
        $versionInfo = $checker->getLatestVersionInfo();
        if (empty($versionInfo)) {
            //Nothing worth sending; the client treats a missing newestVersion as "request failed"
            return 500;
        }

        $logger = new BootupLogger();
        $logger->logBootup($uuidHash, $ipHash);

        $joker = new FunFactGenerator();
        $funFact = $joker->getRandomFact();

        $broadcastLoader = new BroadcastLoader();
        $broadcast = $broadcastLoader->loadBroadcast();

        $response = array_merge($versionInfo, ['fact' => $funFact, 'broadcast' => $broadcast]);

        echo json_encode($response);
        //Deliberately not the ping write's result. This response also carries the kill switch, and
        //the mod throws on any 5xx, so a failed analytics write must not cost a player their
        //version check.
        return 200;
    }
}
