<?php


namespace VoicesOfWynn\Controllers;


class Logout extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        if (isset($_SESSION['user'])) {
            $_SESSION['user']->logout();
        }
        header('Location: /');
    }
}