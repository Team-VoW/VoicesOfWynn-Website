<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\DiscordIntegration\DiscordManager;
use VoicesOfWynn\Models\Website\DiscordRole;
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
        if ($_POST['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_POST['action']) {
            case 'syncUser':
                $imgurl = (isset($_POST['imgurl'])) ? $_POST['imgurl'] : null;
                $name = (isset($_POST['name'])) ? $_POST['name'] : null;
                $rolesJson = (isset($_POST['roles'])) ? $_POST['roles'] : null;

                //Parse the JSON array of role names into array of DiscordRole objects
                if (!is_null($rolesJson)) {
                    $roles = array();
                    $jsonData = json_decode($rolesJson);
                    foreach ($jsonData as $roleName) {
                        $roles[] = new DiscordRole($roleName);
                    }
                } else {
                    $roles = null;
                }

                $responseCode = $manager->syncUser(
                    $_POST['discordId'],
                    $_POST['discordName'],
                    $imgurl,
                    $roles,
                    $name
                );
                
                if ($responseCode === 201) {
                    echo json_encode(['tempPassword' => $manager->lastUserPassword]);
                }
                return $responseCode;
            default:
                return 400;
        }
    }
}
