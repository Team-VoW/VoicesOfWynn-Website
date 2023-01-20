<?php

namespace VoicesOfWynn\Models\Api\DiscordIntegration;

use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\UserException;

class DiscordManager
{

    /**
     * Method echoing all user accounts registered in the system, along with all their information
     * @warning Do not use this function for frequent and automated request, as it puts quite a lot of load on the database
     * @return string JSON-encoded user account list
     * @throws UserException
     */
    public function getAllUsers(): string
    {
        $accountManager = new AccountManager();
        $users = $accountManager->getUsers();
        foreach ($users as $user) {
            $user->load();
        }
        return json_encode($users);
    }

    /**
     * Method updating Discord-related information of a single user
     * @param int $discordId Discord account ID of the user to update
     * @param string $discordName Discord account username of the user to update
     * @param string $avatarUrl URL of the Discord avatar of the user
     * @param DiscordRole[] $discordRoles List of Discord roles that the user should have
     * @param string $displayName Display name of the user for the website
     * @return int HTTP response code
     * @throws UserException
     * @throws \Exception If the $discordRoles argument contains an unknown role
     */
    public function syncUser(int $discordId, string $discordName, string $avatarUrl, array $discordRoles, string $displayName): int
    {
        $accountManager = new AccountManager();

        //Get user by Discord ID
        $user = $accountManager->getUserByDiscordId($discordId);
        if (!$user) {
            //Get user by Discord social
            $user = $accountManager->getUserByDiscordName($discordName);
            if (!$user) {
                //Register new user
                $user = new User();
                $user->registerFromBot($displayName, $discordId);
            }
        }

        $user->updateRoles($discordRoles);

        //TODO: Save the Discord avatar

        return 200;
    }
}

