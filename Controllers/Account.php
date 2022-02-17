<?php


namespace VoicesOfWynn\Controllers;


use VoicesOfWynn\Models\AccountDataValidator;

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
	        default:
	        	return false;
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
	    self::$data['account_name'] = $_SESSION['user']->getName();
        self::$data['account_email'] = $_SESSION['user']->getEmail();
        self::$data['account_publicEmail'] = $_SESSION['user']->hasPublicEmail();
		self::$data['account_discord'] = $_SESSION['user']->getSocial('discord');
		self::$data['account_youtube'] = $_SESSION['user']->getSocial('youtube');
		self::$data['account_twitter'] = $_SESSION['user']->getSocial('twitter');
		self::$data['account_castingcallclub'] = $_SESSION['user']->getSocial('castingcallclub');
        self::$data['account_picture'] = $_SESSION['user']->getAvatarLink();
        self::$data['account_roles'] = $_SESSION['user']->getRoles();
        self::$data['account_bio'] = $_SESSION['user']->getBio();
        if (empty(self::$data['account_error'])) {
            self::$data['account_error'] = array();
        }
        
        self::$views[] = 'account';
        self::$cssFiles[] = 'account';
        self::$jsFiles[] = 'account';
        self::$jsFiles[] = 'tinymce';
        
        return true;
    }
    
    /**
     * Processing method for POST requests to this controller (account info was updated)
     * @param array $args
     * @return bool
     */
    private function post(array $args): bool
    {
	    $displayName = $_POST['name'];
        $password = $_POST['password'];
	    $email = $_POST['email'];
	    $publicEmail = isset($_POST['publicEmail']);
		$discord = $_POST['discord'];
	    $youtube = $_POST['youtube'];
	    $twitter = $_POST['twitter'];
	    $castingcallclub = $_POST['castingcallclub'];
        $bio = $_POST['bio'];
        
        $validator = new AccountDataValidator();
		
        $validator->validateName($displayName);
		
        if (!empty($password)) {
            $validator->validatePassword($password);
        }
	
	    if (!empty($email)) {
		    $validator->validateEmail($email);
	    }
	    else {
		    $email = null;
	    }
	
	    if (!empty($discord)) {
		    $validator->validateDiscord($discord);
	    }
	    else {
		    $discord = null;
	    }
	
	    if (!empty($youtube)) {
		    $validator->validateYouTubeLink($youtube);
	    }
	    else {
		    $youtube = null;
	    }
	
	    if (!empty($twitter)) {
		    $validator->validateTwitter($twitter);
	    }
	    else {
		    $twitter = null;
	    }
	
	    if (!empty($castingcallclub)) {
		    $validator->validateCastingCallClub($castingcallclub);
	    }
	    else {
		    $castingcallclub = null;
	    }
		
	    $bio = $validator->sanitizeBio($bio);
		$validator->validateBio($bio);
		
        if ($_FILES['avatar']['error'] !== UPLOAD_ERR_NO_FILE) {
            $validator->validateAvatar($_FILES['avatar']);
        
            $avatar = $_SESSION['user']->getId();
            switch ($_FILES['avatar']['type']) {
                case 'image/jpeg':
                    $avatar .= '.jpg';
                    break;
                case 'image/png':
                    $avatar .= '.png';
                    break;
            }
        }
        
        if (empty($validator->errors)) {
            if ($_FILES['avatar']['error'] !== UPLOAD_ERR_NO_FILE) {
                //Delete old avatars
                array_map('unlink', glob('dynamic/avatars/'.$_SESSION['user']->getId().'.*'));
                
                //Save changes
                move_uploaded_file($_FILES['avatar']['tmp_name'], 'dynamic/avatars/'.$avatar);
            } else {
                $avatar = $_SESSION['user']->getAvatarLink(false);
            }
            
            $_SESSION['user']->update($email, $password, $displayName, $avatar, $bio, $discord, $youtube, $twitter, $castingcallclub, $publicEmail);
        }
    
        $result = $this->get(array());
    
        self::$data['account_name'] = $displayName;
	    self::$data['account_email'] = $email;
	    self::$data['account_discord'] = $discord;
	    self::$data['account_youtube'] = $youtube;
	    self::$data['account_twitter'] = $twitter;
	    self::$data['account_castingcallclub'] = $castingcallclub;
        //TODO - somhow keep the new and unsaved avatar
        self::$data['account_bio'] = $bio;
	    self::$data['account_error'] = array_merge($validator->errors, $validator->warnings);
        
        return $result;
    }
}

