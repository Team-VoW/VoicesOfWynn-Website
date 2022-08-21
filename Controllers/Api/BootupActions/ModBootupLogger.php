<?php

namespace VoicesOfWynn\Controllers\Api\BootupActions;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\FunFacts\FunFactGenerator;
use VoicesOfWynn\Models\Api\UsageAnalysis\BootupLogger;
use VoicesOfWynn\Models\Api\VersionChecker\VersionChecker;

class ModBootupLogger extends ApiController
{

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }

        //Log the mod bootup
        $uuidHash = @$_GET['id'];
        $ipHash = hash('sha256', $_SERVER['REMOTE_ADDR']);
        if (empty($uuidHash) || empty($ipHash)) {
            //No stats, no fun fact or version check for you
            return 400;
        }
        $logger = new BootupLogger();
        $logResult = $logger->logBootup($uuidHash, $ipHash);

        //Provide version info and fun fact
        $checker = new VersionChecker();
        $versionInfo = $checker->getLatestVersionInfo();

        $joker = new FunFactGenerator();
        $funFact = $joker->getRandomFact();

        $response = array_merge($versionInfo, array('fact' => $funFact));

        echo json_encode($response);
        return $logResult;
    }
}