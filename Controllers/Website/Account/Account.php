<?php


namespace VoicesOfWynn\Controllers\Website\Account;

use VoicesOfWynn\Controllers\Website\WebpageController;
use VoicesOfWynn\Models\Website\AccountDataValidator;
use VoicesOfWynn\Models\Website\User;

class Account extends WebpageController
{

    public const PROFILE_AVATAR_DIRECTORY = 'dynamic/avatars/';
    public const DISCORD_AVATAR_DIRECTORY = 'dynamic/discord-avatars/';

    /**
     * @var User The user object that we're editing
     */
    private User $user;

    /**
     * @inheritDoc
     */
    public function process(array $args): int
    {
        if (!isset($_SESSION['user'])) {
            //No user is logged in
            return 401;
        }

        $userId = $args[0];
        array_shift($args);
        if ($userId === 'self') {
            //Editing logged-in user's profile
            $this->user = $_SESSION['user'];
        } else {
            //Editing somebody else's profile
            //Check whether the logged-in user is a system administrator
            if (!$_SESSION['user']->isSysAdmin()) {
                //The logged user is not system admin
                return 403;
            }

            $this->user = new User();
            $this->user->setData(array('id' => $userId));
            $this->user->load();
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
     * Processing method for GET requests to this controller (account info form was requested)
     * @param array $args
     * @return bool
     */
    private function get(array $args): bool
    {
        self::$data['base_title'] = 'Your Account';
        self::$data['base_description'] = 'Here, you can change the information about you, that is publicly visible.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice,Account,Management';

        self::$data['account_id'] = $this->user->getId();
	    self::$data['account_name'] = $this->user->getName();
        self::$data['account_email'] = $this->user->getEmail();
        self::$data['account_publicEmail'] = $this->user->hasPublicEmail();
		self::$data['account_discord'] = $this->user->getSocial('discord');
		self::$data['account_youtube'] = $this->user->getSocial('youtube');
		self::$data['account_twitter'] = $this->user->getSocial('twitter');
		self::$data['account_castingcallclub'] = $this->user->getSocial('castingcallclub');
        self::$data['account_picture'] = $this->user->getAvatarLink();
        self::$data['account_roles'] = $this->user->getRoles();
        self::$data['account_bio'] = $this->user->getBio();
        if (empty(self::$data['account_error'])) {
            self::$data['account_error'] = array();
        }

        self::$views[] = 'account';
        self::$cssFiles[] = 'account';
        self::$jsFiles[] = 'account';
        self::$jsFiles[] = 'tinymce_bio';

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

        $validator->validateName($displayName, $this->user->getId());

        if (!empty($password)) {
            $validator->validatePassword($password);
        }

	    if (!empty($email)) {
		    $validator->validateEmail($email, $this->user->getId());
	    }
	    else {
		    $email = null;
	    }

	    if (!empty($discord)) {
		    $validator->validateDiscord($discord, $this->user->getId());
	    }
	    else {
		    $discord = null;
	    }

	    if (!empty($youtube)) {
		    $validator->validateYouTubeLink($youtube, $this->user->getId());
	    }
	    else {
		    $youtube = null;
	    }

	    if (!empty($twitter)) {
		    $validator->validateTwitter($twitter, $this->user->getId());
	    }
	    else {
		    $twitter = null;
	    }

	    if (!empty($castingcallclub)) {
		    $validator->validateCastingCallClub($castingcallclub, $this->user->getId());
	    }
	    else {
		    $castingcallclub = null;
	    }

	    $bio = $validator->sanitizeBio($bio);
		$validator->validateBio($bio);

        if ($_FILES['avatar']['error'] !== UPLOAD_ERR_NO_FILE) {
            $validator->validateAvatar($_FILES['avatar']);

            $avatar = $this->user->getId();
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
                array_map('unlink', glob(self::PROFILE_AVATAR_DIRECTORY.$this->user->getId().'.*'));

                //Save changes
                move_uploaded_file($_FILES['avatar']['tmp_name'], self::PROFILE_AVATAR_DIRECTORY.$avatar);
            } else {
                $avatar = $this->user->getAvatar();
            }

            $this->user->update($email, $password, $displayName, $avatar, $bio, $discord, $youtube, $twitter, $castingcallclub, $publicEmail);
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

