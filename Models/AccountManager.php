<?php

namespace VoicesOfWynn\Models;

class AccountManager
{
    public function getUsers(): array
    {
        $userData = Db::fetchQuery('SELECT user_id,picture,display_name,bio FROM user', array(), true);
        $userRoles = Db::fetchQuery('SELECT user_discord_role.user_id,discord_role.name,discord_role.color,discord_role.weight FROM user_discord_role JOIN discord_role ON discord_role.discord_role_id = user_discord_role.discord_role_id ORDER BY user_id ASC',
            array(), true);
        
        $users = array();
        $role_array_itterator = 0;
        foreach ($userData as $userInfo) {
            $roles = array();
            for (; $userRoles[$role_array_itterator]['user_id'] === $userInfo['user_id']; $role_array_itterator++) {
                $roles[] = new DiscordRole($userRoles[$role_array_itterator]['name'],
                    $userRoles[$role_array_itterator]['color'], $userRoles[$role_array_itterator]['weight']);
            }
            $role_array_itterator--;
            $user = new User();
            $user->setData(array(
                'id' => $userInfo['user_id'],
                'displayName' => $userInfo['display_name'],
                'avatarLink' => $userInfo['picture'],
                'bio' => $userInfo['bio'],
            ));
            $user->setRoles($roles);
            $users[] = $user;
        }
        
        return $users;
    }
    
    public function getRoles(): array
    {
        $roles = Db::fetchQuery('SELECT name,color,weight FROM discord_role ORDER BY weight DESC', array(), true);
        $result = array();
        foreach ($roles as $roleInfo) {
            $result[] = new DiscordRole($roleInfo['name'], $roleInfo['color'], $roleInfo['weight']);
        }
        
        return $result;
    }
}

