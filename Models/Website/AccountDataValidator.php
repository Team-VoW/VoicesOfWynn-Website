<?php

namespace VoicesOfWynn\Models\Website;

use HTMLPurifier;
use HTMLPurifier_Config;
use VoicesOfWynn\Models\Db;

class AccountDataValidator
{
    private const EMAIL_MAX_LENGTH = 255;
    public const PASSWORD_MIN_LENGTH = 6;
    private const NAME_MAX_LENGTH = 31;
    private const NAME_MIN_LENGTH = 3;
	private const DISCORD_NAME_MAX_LENGTH = 32;
	private const DISCORD_NAME_MIN_LENGTH = 2;
	private const YOUTUBE_NAME_MAX_LENGTH = 56;
	private const YOUTUBE_NAME_MIN_LENGTH = 14; //Length of youtube.com/c/
	private const TWITTER_NAME_MAX_LENGTH = 15; //Not including @
	private const TWITTER_NAME_MIN_LENGTH = 1;
	private const CCC_NAME_MAX_LENGTH = 64; //Don't know the exact limit
	private const CCC_NAME_MIN_LENGTH = 1;  //Don't know the exact limit
    private const AVATAR_MAX_SIZE = 1048576; //In bytes
    private const BIO_MAX_LENGTH = 65535;

    public array $errors = array();
    public array $warnings = array();

    public function validateEmail(string $email, int $allowDuplicateForUserId): bool
    {
        //Check length
        if (mb_strlen($email) > self::EMAIL_MAX_LENGTH) {
            $this->errors[] = 'E-mail address mustn\'t be more than '.self::EMAIL_MAX_LENGTH.' characters long.';
            return false;
        }

        //Check format (might not allow some exotic but valid e-mail domains)
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
            $this->errors[] = 'E-mail address doesn\'t seem to be in the correct format. If you are sure that you entered your e-mail address properly, ping shady_medic on Discord.';
            return false;
        }

        //Check uniqueness
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE email = ? AND user_id != ?',
            array($email, $allowDuplicateForUserId));
        if ($result['cnt'] > 0) {
            $this->errors[] = 'This e-mail address is already in use.';
            return false;
        }

        return true;
    }

    public function validatePassword(string $password): bool
    {
        //Check length
        if (mb_strlen($password) < self::PASSWORD_MIN_LENGTH) {
            $this->errors[] = 'Password must be at least '.self::PASSWORD_MIN_LENGTH.' characters long.';
            return false;
        }

        /*
         No other checks are needed - only stupid developers limit the users and make them create passwords that are
         hard to remember for humans, but easy to guess for computers (short with many weird characters).
        */

        return true;
    }

    /**
     * Method validating display name
     * @param string $name Name to validate
     * @param int $allowDuplicateForUserId ID of the user, whose display name is allowed to be the same as the name that
     * was chosen by the currently logged-in user. 0 to check against all user names. This value should be set to the
     * user ID of the user, whose account is being edited to allow changes in capitalizasion. Set this to 0 for account
     * creation by system administrators.
     * @return bool TRUE, if the name is valid
     * @throws \Exception
     */
    public function validateName(string $name, int $allowDuplicateForUserId): bool
    {
        //Check length
        if (mb_strlen($name) > self::NAME_MAX_LENGTH) {
            $this->errors[] = 'Display name mustn\'t be more than '.self::NAME_MAX_LENGTH.' characters long.';
            return false;
        }

        if (mb_strlen($name) < self::NAME_MIN_LENGTH) {
            $this->errors[] = 'Display name mustn\'t be less than '.self::NAME_MIN_LENGTH.' characters long.';
            return false;
        }

        //Check uniqueness
        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(display_name) = ? AND user_id != ?',
            array(strtoupper($name), $allowDuplicateForUserId));
        if ($result['cnt'] > 0) {
            $this->errors[] = 'This display name is already in use.';
            return false;
        }

        return true;
    }

    public function validateAvatar(array $uploadInfo): bool
    {
        if ($uploadInfo['error'] === UPLOAD_ERR_FORM_SIZE || $uploadInfo['size'] > self::AVATAR_MAX_SIZE) {
            $this->errors[] = 'The profile image must be smaller than 1 MB.';
            return false;
        }
        if ($uploadInfo['type'] !== 'image/png' && $_FILES['avatar']['type'] !== 'image/jpeg') {
            $this->errors[] = 'The profile image must be either .PNG or .JPG.';
            return false;
        }
        if ($uploadInfo['error'] !== 0) {
            $this->errors[] = 'An unknown error occurred during the file upload – try again or ping shady_medic on Discord.';
            return false;
        }

        return true;
    }

    public function validateBio(string $bio): bool
    {
        //Check length
        if (strlen($bio) > self::BIO_MAX_LENGTH) { //Not using mb_strlen because I need to count single-bit characters for the database limit
            $this->errors[] = 'Bio mustn\'t be more than '.self::BIO_MAX_LENGTH.' characters long (including formatting tags). Your current bio is '.strlen($bio).' characters long.';
            return false;
        }

         return true;
    }
	
	public function validateDiscord(string $discordName, int $allowDuplicateForUserId): bool
	{
		//Check length
		if (mb_strlen($discordName) > self::DISCORD_NAME_MAX_LENGTH) {
			$this->errors[] = 'Discord username mustn\'t be more than '.self::DISCORD_NAME_MAX_LENGTH.' characters long.';
			return false;
		}
		
		if (mb_strlen($discordName) < self::DISCORD_NAME_MIN_LENGTH) {
			$this->errors[] = 'Discord username mustn\'t be less than '.self::DISCORD_NAME_MIN_LENGTH.' characters long.';
			return false;
		}
		
		//Check uniqueness
		$result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(discord) = ? AND user_id != ?',
			array(strtoupper($discordName), $allowDuplicateForUserId));
		if ($result['cnt'] > 0) {
			$this->errors[] = 'This Discord username is already in use.';
			return false;
		}
		
		//Check format
		if (preg_match("/^[0-9a-z_.]*$/", $discordName) !== 1 || strpos($discordName, '..') !== false) {
			$this->errors[] = 'This Discord username is in incorrect format.';
			return false;
		}
		
		return true;
	}
	
	public function validateYouTubeLink(string $youtubeLink, int $allowDuplicateForUserId): bool
	{
		//Check length
		if (mb_strlen($youtubeLink) > self::YOUTUBE_NAME_MAX_LENGTH) {
			$this->errors[] = 'YouTube channel link mustn\'t be more than '.self::YOUTUBE_NAME_MAX_LENGTH.' characters long.';
			return false;
		}
		
		if (mb_strlen($youtubeLink) < self::YOUTUBE_NAME_MIN_LENGTH) {
			$this->errors[] = 'YouTube channel link mustn\'t be less than '.self::YOUTUBE_NAME_MIN_LENGTH.' characters long.';
			return false;
		}
		
		//Check uniqueness
		$result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(youtube) = ? AND user_id != ?',
			array(strtoupper($youtubeLink), $allowDuplicateForUserId));
		if ($result['cnt'] > 0) {
			$this->errors[] = 'This YouTube channel is already linked by another user.';
			return false;
		}
		
		//Check format
		if (preg_match("/^(http(s)?:\/\/(www\.)?)?youtube\.com\/(c(hannel)?\/)?[^\/]*$/", $youtubeLink) !== 1) {
			$this->errors[] = 'The YouTube channel link is in incorrect format.';
			return false;
		}
		
		//Check if the channel exists
		$handle = curl_init($youtubeLink);
		curl_setopt($handle, CURLOPT_RETURNTRANSFER, true);
		curl_exec($handle);
		$httpCode = curl_getinfo($handle, CURLINFO_HTTP_CODE);
		if($httpCode !== 200) {
			$this->errors[] = 'YouTube channel wasn\'t found.';
			return false;
		}
		curl_close($handle);
		
		return true;
	}
	
	public function validateTwitter(string $twitterHandle, int $allowDuplicateForUserId): bool
	{
		//Check length
		if (mb_strlen($twitterHandle) > self::TWITTER_NAME_MAX_LENGTH) {
			$this->errors[] = 'Twitter handle mustn\'t be more than '.self::TWITTER_NAME_MAX_LENGTH.' characters long.';
			return false;
		}
		
		if (mb_strlen($twitterHandle) < self::TWITTER_NAME_MIN_LENGTH) {
			$this->errors[] = 'Twitter handle mustn\'t be less than '.self::TWITTER_NAME_MIN_LENGTH.' characters long.';
			return false;
		}
		
		//Check uniqueness
		$result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(twitter) = ? AND user_id != ?',
			array(strtoupper($twitterHandle), $allowDuplicateForUserId));
		if ($result['cnt'] > 0) {
			$this->errors[] = 'This Twitter account is already linked by another user.';
			return false;
		}
		/*
		//TODO - try to find a way to make this work, probably with official Twitter API
		//Update: Nevermind, now when Twitter is Elon's paywalled sh*thole, I won't bother lol
		//Check if the channel exists
		$handle = curl_init('https://twitter.com/'.$twitterHandle);
		curl_setopt($handle, CURLOPT_RETURNTRANSFER, true);
		curl_exec($handle);
		$httpCode = curl_getinfo($handle, CURLINFO_HTTP_CODE);
		if($httpCode !== 200) {
			$this->errors[] = 'Twitter account wasn\'t found.';
			return false;
		}
		curl_close($handle);
		*/
		return true;
	}
	
	public function validateCastingCallClub(string $castingCallClubName, int $allowDuplicateForUserId): bool
	{
		//Check length
		if (mb_strlen($castingCallClubName) > self::CCC_NAME_MAX_LENGTH) {
			$this->errors[] = 'Casting Call Club name mustn\'t be more than '.self::CCC_NAME_MAX_LENGTH.' characters long.';
			return false;
		}
		
		if (mb_strlen($castingCallClubName) < self::CCC_NAME_MIN_LENGTH) {
			$this->errors[] = 'Casting Call Club name mustn\'t be less than '.self::CCC_NAME_MIN_LENGTH.' characters long.';
			return false;
		}
		
		//Check uniqueness
		$result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(castingcallclub) = ? AND user_id != ?',
			array(strtoupper($castingCallClubName), $allowDuplicateForUserId));
		if ($result['cnt'] > 0) {
			$this->errors[] = 'This Casting Call Club account is already linked by another user.';
			return false;
		}
		
		//Check if the channel exists
		$handle = curl_init('https://www.castingcall.club/'.$castingCallClubName);
		curl_setopt($handle,  CURLOPT_RETURNTRANSFER, TRUE);
		curl_exec($handle);
		$httpCode = curl_getinfo($handle, CURLINFO_HTTP_CODE);
		if($httpCode !== 200) {
			$this->errors[] = 'Casting Call Club account wasn\'t found.';
			return false;
		}
		curl_close($handle);
		
		return true;
	}
  
  public function sanitizeBio(string $bio): string
	{
        /* NOTE: This function is also used to sanitize changelogs for downloads written by admins */

		//Unify linebreaks
		$bio = str_replace('<br>', '<br />', $bio);
		
		//Run bio through tag and attribute whitelist
        $config = HTMLPurifier_Config::createDefault();
        $config->set('HTML.DefinitionID', 'enduser-customize.html tutorial');
		$config->set('HTML.DefinitionRev', 1);
		//$config->set('Cache.DefinitionImpl', null); // TODO: remove this later!

		$config->set('HTML.Allowed', 'p[style],span[style],strong,em,ul,ol,li,h1,h2,h3,a[title|href|target|rel],img[src|alt|width|height],br');
		$config->set('Attr.AllowedClasses', '');
		$config->set('CSS.MaxImgLength', '800px');
		$config->set('CSS.AllowedFonts', '');
		$config->set('CSS.AllowedProperties', array('text-align','text-decoration'));
		if ($def = $config->maybeGetRawHTMLDefinition()) {
			$def->addAttribute('span', 'data-mce-style', 'Text');
			$def->addAttribute('a', 'data-mce-href', 'Text');
			$def->addAttribute('a', 'data-mce-selected', 'Text');
			$def->addAttribute('a', 'target', 'Text');
			$def->addAttribute('a', 'rel', 'Text');
			$def->addAttribute('img', 'data-mce-src', 'Text');
			$def->addAttribute('img', 'data-mce-selected', 'Text');
		}
		
		$purifier = new HTMLPurifier($config);
		$result = $purifier->purify($bio);
		
		if (str_replace(' ', '', $result) !== str_replace(' ', '', $bio)) { //HTMLpurifier sometimes removes spaces in the "style" atribute
			$this->warnings[] = 'It seems like your bio contains disallowed HTML code. If you used only the tools provided in the toolbar and see unwanted changes, ping shady_medic on Discord please.';
		}
		return $result;
  }
}

