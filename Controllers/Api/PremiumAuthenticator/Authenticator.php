<?php

namespace VoicesOfWynn\Controllers\Api\PremiumAuthenticator;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\PremiumAuthenticator\PremiumCodeManager;

class Authenticator extends ApiController
{
    public function process(array $args): int
    {
        $apiKey = $_REQUEST['apiKey'] ?? null;
        if ($apiKey !== self::PREMIUM_AUTHENTICATOR_API_KEY) {
            return 401;
        }

        switch ($args[0]) {
            case 'get-code':
                $discordUserId = $_REQUEST['discord'] ?? null;
                if (is_null($discordUserId)) {
                    return 400;
                }

                $code = $this->getCodeForUser($discordUserId);
                $status = 200;
                if (is_null($code)) {
                    $code = $this->generateCodeForUser($discordUserId);
                    $status = 201;
                }
                echo json_encode(["code" => $code]);
                return $status;
            case 'check-code':
                $valid = $this->checkCode();
                if ($valid > 1) { //Higher numbers are HTTP error codes
                    return $valid;
                }
                echo json_encode(['valid' => ($valid) ? 'true' : 'false']);
                return 200;
            default:
                return 400;
        }
    }

    private function getCodeForUser(string $discordUserId) : ?string
    {
        $manager = new PremiumCodeManager();
        return $manager->getCode($discordUserId);
    }

    private function generateCodeForUser(string $discordUserId) : string
    {
        $manager = new PremiumCodeManager();
        return $manager->createNew($discordUserId);
    }

    private function checkCode() : int
    {
        $code = $_REQUEST['code'] ?? null;
        if (is_null($code)) {
            return 400;
        }

        $manager = new PremiumCodeManager();
        return $manager->verify($code);
    }
}

