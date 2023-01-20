<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\DiscordIntegration\DiscordManager;

class DiscordIntegration extends ApiController
{
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->processGet();
            case 'POST':
                return $this->processPost();
            default:
                return 405;
        }
    }

    private function processGet(): int
    {
        if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_GET['action']) {
            case 'getAllUsers':
                return $manager->getAllUsers();
            default:
                return 400;
        }
    }

    private function processPost(): int
    {
        if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_GET['action']) {
            case 'syncUser':
                return $manager->syncUser();
            default:
                return 400;
        }
    }
}