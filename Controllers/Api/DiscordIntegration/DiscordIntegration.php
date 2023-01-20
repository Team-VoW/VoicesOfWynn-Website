<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\DiscordIntegration\DiscordManager;
use VoicesOfWynn\Models\Website\UserException;

class DiscordIntegration extends ApiController
{
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get();
            case 'POST':
                return $this->post();
            default:
                return 405;
        }
    }

    /**
     * @throws UserException
     */
    private function get(): int
    {
        if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_GET['action']) {
            case 'getAllUsers':
                $users = $manager->getAllUsers();
                echo $users;
                return 200;
            default:
                return 400;
        }
    }

    /**
     * @throws UserException
     */
    private function post(): int
    {
        if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_GET['action']) {
            case 'syncUser':
                return $manager->syncUser(
                    $_POST['discordId'],
                    $_POST['discordName'],
                    $_POST['imgurl'],
                    $_POST['roles'],
                    $_POST['name']
                );
            default:
                return 400;
        }
    }
}