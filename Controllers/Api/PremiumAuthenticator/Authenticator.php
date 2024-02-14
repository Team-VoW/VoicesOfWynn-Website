<?php

namespace VoicesOfWynn\Controllers\Api\PremiumAuthenticator;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\PremiumAuthenticator\PremiumCodeManager;

class Authenticator extends ApiController
{
    const STREAM_SERVER_IP = '127.0.0.1';

    public function process(array $args): int
    {
        parse_str(file_get_contents("php://input"),$_INPUT);
        $_INPUT = array_merge($_REQUEST, $_INPUT);

        $apiKey = $_INPUT['apiKey'] ?? null;
        $discordUserId = $_INPUT['discord'] ?? null;

        if (!in_array($args[0], ['check-code'])) {
            if ($apiKey !== self::PREMIUM_AUTHENTICATOR_API_KEY) {
                return 401;
            }

            if (is_null($discordUserId)) {
                return 400;
            }
        }

        switch ($args[0]) {
            case 'check-code':
                if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
                    return 405;
                }

                $valid = $this->checkCode();
                $result = ['valid' => ($valid === 200) ? 'true' : 'false'];
                if ($valid === 200) {
                    $result['ip'] = self::STREAM_SERVER_IP;
                } else {
                    $result['reason'] = ($valid === 402) ? 'expired' : 'invalid';
                }
                echo json_encode($result);
                return 200;
            case 'get-code':
                if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
                    return 405;
                }

                $result = $this->getCodeForUser($discordUserId);
                $status = $result['status'];
                $code = $result['code'] ?? null;
                if ($status === 404) {
                    $code = $this->generateCodeForUser($discordUserId);
                    $status = 201;
                }
                echo json_encode(["code" => $code]);
                return $status;
            case 'deactivate-user':
                if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
                    return 405;
                }

                return ($this->deactivateCodeForUser($discordUserId)) ? 204 : 500;
            case 'reactivate-user':
                if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
                    return 405;
                }

                return ($this->activateCodeForUser($discordUserId)) ? 204 : 500;
            default:
                return 400;
        }
    }

    private function getCodeForUser(string $discordUserId): array
    {
        $manager = new PremiumCodeManager();
        $codeInfo = $manager->getCode($discordUserId);
        if (is_null($codeInfo)) {
            return ['status' => 404];
        } else if (!$codeInfo['active']) {
            return ['status' => 402, 'code' => $codeInfo['code']];
        } else {
            return ['status' => 200, 'code' => $codeInfo['code']];
        }
    }

    private function generateCodeForUser(string $discordUserId): string
    {
        $manager = new PremiumCodeManager();
        return $manager->createNew($discordUserId);
    }

    private function checkCode(): int
    {
        $code = $_REQUEST['code'] ?? null;
        if (is_null($code) || strlen($code) !== 16) {
            return 400;
        }

        $manager = new PremiumCodeManager();
        return $manager->verify($code);
    }

    private function deactivateCodeForUser(string $discordUserId): bool
    {
        $manager = new PremiumCodeManager();
        return $manager->deactivate($discordUserId);
    }

    private function activateCodeForUser(string $discordUserId): bool
    {
        $manager = new PremiumCodeManager();
        return $manager->activate($discordUserId);
    }
}

