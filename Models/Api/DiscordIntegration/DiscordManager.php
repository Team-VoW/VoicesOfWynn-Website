<?php

namespace VoicesOfWynn\Models\Api\DiscordIntegration;

use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\User;

class DiscordManager
{
    public function getAllUsers(): int
    {
        $accountManager = new AccountManager();
        $users = $accountManager->getUsers();
        foreach ($users as $user) {
            $user->load();
        }
        echo json_encode($users);
        return 200;
    }

    public function syncUser(): int
    {
        $discordId = $_POST['discordId'];
        $rolesDiscord = $_POST['roles'];
        $discordName = $_POST['discordName'];
        // Open for suggestion on how to implement the auto addition of the avatar
        //$imgUrl    = $_POST['imgurl'];
        $displayName = $_POST['name'];

        $accountManager = new AccountManager();
        $users = $accountManager->getUsers();
        if ($accountManager->checkUserExistsByDiscordId($discordId)) {
            foreach ($users as $user) {
                $user->load();
                if ($user->getDiscordId() === $discordId) {
                    $roles = $user->getRoles();
                    foreach ($roles as $role) {
                        $user->removeRole($role->name);
                    }

                    foreach ($rolesDiscord as $role) {
                        $r = new DiscordRole($role);
                        $user->addRole($r->getId());
                    }
                }
            }
            echo "allready exists";
            return 200;
        } else if ($accountManager->checkUserExistsByDiscordName($discordName)) {
            foreach ($users as $user) {
                $user->load();
                if ($user->getSocial("discord") === $discordName) {
                    $roles = $user->getRoles();
                    foreach ($roles as $role) {
                        $user->removeRole($role->name);
                    }

                    foreach ($rolesDiscord as $role) {
                        $r = new DiscordRole($role);
                        $user->addRole($r->getId());
                    }
                }
            }
            echo "allready exists";
            return 200;
        }
        $user = new User();
        $user->registerFromBot($displayName, $discordId);
        $roles = $user->getRoles();
        foreach ($roles as $role) {
            $user->removeRole($role->name);
        }

        foreach ($rolesDiscord as $role) {
            $r = new DiscordRole($role);
            $user->addRole($r->getId());
        }
        return 200;
    }
}

