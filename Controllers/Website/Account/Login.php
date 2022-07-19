<?php


namespace VoicesOfWynn\Controllers\Website\Account;


use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\User;
use VoicesOfWynn\Models\Website\UserException;

class Login extends WebpageController
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): int
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
            default:
                return 405;
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
        
        if (empty(self::$data['login_username'])) {
            self::$data['login_username'] = '';
        }
        if (empty(self::$data['login_error'])) {
            self::$data['login_error'] = '';
        }
        if (empty(self::$data['login_change_password'])) {
            self::$data['login_change_password'] = false;
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
        $name = @$_POST['name'];
        $pass = @$_POST['password'];
        $newpass = @$_POST['newPassword'];
    
        $user = new User();
        
        try {
            if (!empty($newpass)) {
                self::$data['login_username'] = $_SESSION['passchangename']; //In case of an exception being thrown
                self::$data['login_change_password'] = true; //In case of an exception being thrown
                if ($user->changeTempPassword(@$_SESSION['passchangename'], $newpass)) {
                    header('Location: /account');
                    return true;
                }
                return 400;
            }
            else if ($user->login($name, $pass)) {
                //Login was successful
                header('Location: /account');
                return true;
            }
            else {
                //Login was successful, but the password needs to be changed
                self::$data['login_username'] = $name;
                self::$data['login_change_password'] = true;
                $code = bin2hex(random_bytes(8));
                setcookie('passchangecode', $code, 0, '/');
                $_SESSION['passchangecode'] = $code;
                $_SESSION['passchangename'] = $name;
                return $this->get(array());
            }
        } catch (UserException $e) {
            self::$data['login_error'] = $e->getMessage();
            self::$data['login_username'] = $name;
            return $this->get(array());
        }
    }
}

