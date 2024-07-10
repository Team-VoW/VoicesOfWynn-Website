<?php


namespace VoicesOfWynn\Controllers\Website\Account;


use VoicesOfWynn\Controllers\Controller;

class Logout extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if (isset($_SESSION['user'])) {
            $_SESSION['user']->logout();
        }
        header('Location: /');
        exit();
    }
}