<?php


namespace VoicesOfWynn\Controllers;


class Account extends Controller
{
    
    /**
     * @inheritDoc
     */
    public function process(array $args): bool
    {
        if (!isset($_SESSION['user'])) {
            //No user is logged in
            $errorController = new Error403();
            return $errorController->process(array());
        }
        
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get(array());
            case 'POST':
                return $this->post(array());
        }
    }
    
    /**
     * Processing method for GET requests to this controller (account info form was requested)
     * @param array $args
     * @return bool
     */
    private function get(array $args): bool
    {
        self::$data['base_title'] = 'Your Account';
        self::$data['base_description'] = 'Here, you can change the information about you, that is publicly visible.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Account,Management';
        
        self::$data['account_id'] = $_SESSION['user']->getId();
        self::$data['account_email'] = $_SESSION['user']->getEmail();
        self::$data['account_name'] = $_SESSION['user']->getName();
        self::$data['account_picture'] = $_SESSION['user']->getAvatarLink().'?'.rand(0,31);
        self::$data['account_bio'] = $_SESSION['user']->getBio();
        self::$data['account_error'] = '';
        
        self::$views[] = 'account';
        self::$cssFiles[] = 'account';
        self::$jsFiles[] = 'account';
        
        return true;
    }
    
    /**
     * Processing method for POST requests to this controller (account info was updated)
     * @param array $args
     * @return bool
     */
    private function post(array $args): bool
    {
        if ($_FILES['avatar']['size'] > 1048576) {
            self::$data['account_error'] = 'The profile image must be smaller than 1 MB.';
        }
        if ($_FILES['avatar']['type'] !== 'image/jpeg' && $_FILES['avatar']['type'] !== 'image/png') {
            self::$data['account_error'] = 'The profile image must be either .PNG or .JPG.';
        }
        if ($_FILES['avatar']['error'] !== 0) {
            self::$data['account_error'] = 'An unknown error occurred during the file upload - try again or ping @Shady on Discord';
        }
        $fileName = $_SESSION['user']->getId();
        switch ($_FILES['avatar']['type']) {
            case 'image/jpeg':
                $fileName .= '.jpg';
                break;
            case 'image/png':
                $fileName .= '.png';
                break;
        }
        //Delete old avatars
        if (file_exists('dynamic/avatars/'.$_SESSION['user']->getId().'.png')) {
            unlink('dynamic/avatars/'.$_SESSION['user']->getId().'.png');
        }
        if (file_exists('dynamic/avatars/'.$_SESSION['user']->getId().'.jpg')) {
        unlink('dynamic/avatars/'.$_SESSION['user']->getId().'.jpg');
        }
        $avatar = $fileName;
        
        //TODO - validate all other values
        
        $email = $_POST['email'];
        $password = $_POST['password'];
        $displayName = $_POST['name'];
        $bio = $_POST['bio'];
        
        if (empty(self::$data['account_error'])) {
            move_uploaded_file($_FILES['avatar']['tmp_name'], 'dynamic/avatars/'.$fileName);
            $_SESSION['user']->update($email, $password, $displayName, $avatar, $bio);
        }
        $result = $this->get(array());
        self::$data['account_email'] = $email;
        self::$data['account_name'] = $displayName;
        self::$data['account_bio'] = $bio;
        return $result;
    }
}

