<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class AccountManager
{
    /**
     * Summary of getUsers
     * @return User[]
     */
    public function getUsers(): array
    {
        $db = new Db('Website/DbInfo.ini');
        $userData = $db->fetchQuery(
            'SELECT user_id,picture,display_name,bio,discord_id FROM user ORDER BY user_id ASC',
            array(),
            true
        );
        $userRoles = $db->fetchQuery(
            'SELECT user_discord_role.user_id,discord_role.name,discord_role.color,discord_role.weight FROM user_discord_role JOIN discord_role ON discord_role.discord_role_id = user_discord_role.discord_role_id ORDER BY user_id ASC',
            array(),
            true
        );

        // Handle case where queries return false
        if ($userData === false) {
            return array();
        }
        if ($userRoles === false) {
            $userRoles = array();
        }

        $users = array();
        $role_array_itterator = 0;
        foreach ($userData as $userInfo) {
            $roles = array();
            if ($role_array_itterator < count($userRoles)) {
                //There are more roles to assign
                if ($userRoles[$role_array_itterator]['user_id'] !== $userInfo['user_id']) {
                    //This user has no roles, the loop below won't execute and $role_array_itterator will stay the same
                    $skip = true; //To prevent it from going negative
                } else {
                    $skip = false;
                }

                for (
                ;
                    $role_array_itterator < count($userRoles) &&
                    $userRoles[$role_array_itterator]['user_id'] === $userInfo['user_id'];
                    $role_array_itterator++
                ) {
                    $roles[] = new DiscordRole(
                        $userRoles[$role_array_itterator]['name'],
                        $userRoles[$role_array_itterator]['color'], $userRoles[$role_array_itterator]['weight']
                    );
                }

                if (!$skip) {
                    $role_array_itterator--;
                }
            }

            $user = new User();
            $user->setData(
                array(
                    'id' => $userInfo['user_id'],
                    'displayName' => $userInfo['display_name'],
                    'avatarLink' => $userInfo['picture'],
                    'bio' => $userInfo['bio'],
                    'discordId' => $userInfo['discord_id']
                )
            );
            $user->setRoles($roles);
            $users[] = $user;
        }

        return $users;
    }

    /**
     * Method getting a user object from the database by Discord account ID
     * @param int $discordId Discord ID of the user
     * @return User|false User object with all the information from the database or FALSE, if no such user exists
     */
    public function getUserByDiscordId(int $discordId)
    {
        $userData = (new Db('Website/DbInfo.ini'))->fetchQuery(
            'SELECT * FROM user WHERE `discord_id` = ? LIMIT 1',
            array($discordId)
        );

        if (empty($userData)) {
            return false;
        }

        $user = new User();
        $user->setData($userData);

        return $user;
    }

    /**
     * Method getting a user object from the database by Discord username
     * @param string $discordName Discord username of the user
     * @return User|false User object with all the information from the database or FALSE, if no such user exists
     */
    public function getUserByDiscordName(string $discordName)
    {
        $userData = (new Db('Website/DbInfo.ini'))->fetchQuery(
            'SELECT * FROM user WHERE `discord` = ? LIMIT 1',
            array($discordName)
        );

        if (empty($userData)) {
            return false;
        }

        $user = new User();
        $user->setData($userData);

        return $user;
    }

    /**
     * Method returning the list of Discord roles saved in the database, sorted by their weight (in descending order)
     * @return DiscordRole[] List of the roles as objects
     */
    public function getRoles(): array
    {
        $roles = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT `name`,`color`,`weight` FROM discord_role ORDER BY weight DESC', array(), true);
        $result = array();
        foreach ($roles as $roleInfo) {
            $result[] = new DiscordRole($roleInfo['name'], $roleInfo['color'], $roleInfo['weight']);
        }

        return $result;
    }
}

