<?php


namespace VoicesOfWynn\Controllers\Website\Account;


use VoicesOfWynn\Controllers\Website\WebpageController;

class Login extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        header('Location: '.\VoicesOfWynn\appLoginUrl(), true, 302);
        exit();
    }
}
