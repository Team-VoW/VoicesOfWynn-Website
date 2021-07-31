<?php


namespace VoicesOfWynn\Controllers;


use VoicesOfWynn\Models\User;
use VoicesOfWynn\Models\UserException;

class Login extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        if (isset($_SESSION['user'])) {
            //The user is already logged in
            header('Location: /account');
            exit();
        }
        
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
                break;
        }
    }
    
    /**
     * Processing method for GET requests to this controller (login form was requested)
     * @param array $args
     * @return bool
     */
    private function get(array $args): bool
    {
        self::$data['base_title'] = 'Login';
        self::$data['base_description'] = 'Did you contribute to this project? Then login here to change your display name, bio or profile picture.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Login';
        
        if (empty(self::$data['login_error'])) {
            self::$data['login_error'] = '';
        }
        
        self::$views[] = 'login';
        return true;
    }
    
    /**
     * Processing method for POST requests to this controller (login form was submitted)
     * @param array $args
     * @return bool
     */
    private function post(array $args): bool
    {
        $name = $_POST['name'];
        $pass = $_POST['password'];
        
        $user = new User();
        try {
            if ($user->login($name, $pass)) {
                //Login was successful
                header('Location: /account');
                return true;
            }
        } catch (UserException $e) {
            self::$data['login_error'] = $e->getMessage();
            return $this->get(array());
        }
    }
}