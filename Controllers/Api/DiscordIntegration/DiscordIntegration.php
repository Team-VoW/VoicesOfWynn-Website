<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Website\AccountManager;
use VoicesOfWynn\Models\Website\User;

class DiscordIntegration extends ApiController 
{
  public function process(Array $args):int
  {
    switch($_SERVER['REQUEST_METHOD'])
    {
      case 'GET':
        return $this->processGet();
      case 'POST':
        return $this->processPost();
      default:
        return 405;
    }
  }

  private function processGet():int
  {
    if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
      return 401;
    }
    switch ($_GET['action']) {
      case 'getAllUsers':
        return $this->getAllUsers();
      default:
        return 400;
    }
  }
  
  private function processPost():int
  {
    if ($_GET['apiKey'] !== self::DISCORD_INTEGRATION_API_KEY) {
      return 401;
    }
    switch ($_GET['action']) {
      case 'syncUser':
        return $this->syncUser();
      default:
        return 400;
    }
  }

  private function getAllUsers():int
  {
    $accountManager = new AccountManager();
    $users          = $accountManager->getUsers();
    foreach($users as $user) {
      $user->load();
    }
    echo json_encode($users);
    return 200;
  }

  private function syncUser():int
  {
    $discordId = $_POST['discordId'];
    $role = $_POST['role'];
    $imgUrl    = $_POST['imgurl'];
    $displayName = $_POST['name'];

    $accountManager = new AccountManager();
    $users          = $accountManager->getUsers();
    if($accountManager->checkUserExistsByDiscordId($discordId))
    {
      foreach($users as $user) {
        $user->load();
      }
      echo "allready exists";
      return 200;
    }
    $user = new User();
    $user->register($displayName);
    return 200;
  }
}