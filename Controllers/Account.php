<?php


namespace VoicesOfWynn\Controllers;


class Account extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        self::$data['base_title'] = 'Your Account';
        self::$data['base_description'] = 'Here, you can change the information about you, that is publicly visible.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Account,Management';
    
        if (!isset($_SESSION['user'])) {
            //No user is logged in
            $errorController = new Error403();
            return $errorController->process(array());
        }
        
        self::$data['account_id'] = $_SESSION['user']->getId();
        self::$data['account_email'] = $_SESSION['user']->getEmail();
        self::$data['account_name'] = $_SESSION['user']->getName();
        self::$data['account_picture'] = $_SESSION['user']->getAvatarLink();
        self::$data['account_bio'] = $_SESSION['user']->getBio();
        
        self::$views[] = 'account';
        return true;
    }
}