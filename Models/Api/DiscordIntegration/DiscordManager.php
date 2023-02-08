<?php

namespace VoicesOfWynn\Models\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Website\Account\Account;
use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\UserException;

class DiscordManager
{

    /**
     * @var string $lastUserPassword The generated password of the last user registered using its Discord ID
     */
    public string $lastUserPassword;
    
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
     * @param string|null $avatarUrl URL of the Discord avatar of the user, use NULL to keep the current one
     * @param DiscordRole[]|null $discordRoles List of Discord roles that the user should have, use NULL to keep the current one
     * @param string|null $displayName Display name of the user for the website, use NULL to keep the current one
     * @return int HTTP response code
     * @throws UserException
     * @throws \Exception If the $discordRoles argument contains an unknown role
     */
    public function syncUser(int $discordId, string $discordName, ?string $avatarUrl = null, ?array $discordRoles = null, ?string $displayName = null): int
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
                try {
                    $this->lastUserPassword = $user->registerFromBot($displayName, $discordId);
                } catch (UserException $e) {
                    echo json_encode(['error' => $e->getMessage()]);
                    return 500;
                }
            }
        }

        $result = true;

        if (!is_null($avatarUrl)) {
            $result = $this->updateDiscordAvatar($user->getId(), $avatarUrl);
        }

        if (!is_null($discordRoles)) {
            $result = $result && $user->updateRoles($discordRoles);
        }

        if ($result) {
            return (empty($this->lastUserPassword)) ? 200 : 201;
        }

        return 500;
    }

    /**
     * Method downloading an avatar image from a specified URL, saving it in a specific directory
     * and linking it to the profile of a specific user
     * @param int $userId ID of the user to which the avatar should belong; will be used in the file name
     * @param string $avatarUrl Direct URL from which the avatar can be downloaded
     * @return bool TRUE if everything succeeds, FALSE otherwise
     */
    private function updateDiscordAvatar(int $userId, string $avatarUrl): bool
    {
        $fh = fopen(Account::DISCORD_AVATAR_DIRECTORY.$userId.'.png', 'w'); //Also clears the current file, if it exists
        $ch = curl_init();

        curl_setopt($ch, CURLOPT_URL, $avatarUrl);
        curl_setopt($ch, CURLOPT_FILE, $fh);
        curl_setopt($ch, CURLOPT_HEADER, false);
        curl_setopt($ch, CURLOPT_USERAGENT, 'Voices of Wynn +https://www.voicesofwynn.com');
        curl_exec($ch);
        $error = curl_errno($ch);

        curl_close($ch);
        fclose($fh);

        if ($error === 0) {
            return true;
        }
        return false;
    }
}

