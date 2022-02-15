<?php

namespace VoicesOfWynn\Models;

use HTMLPurifier;
use HTMLPurifier_Config;

class AccountDataValidator
{
    private const EMAIL_MAX_LENGTH = 255;
    public const PASSWORD_MIN_LENGTH = 6;
    private const NAME_MAX_LENGTH = 31;
    private const NAME_MIN_LENGTH = 3;
    private const AVATAR_MAX_SIZE = 1048576; //In bytes
    private const BIO_MAX_LENGTH = 65535;
    
    public array $errors = array();
    public array $warnings = array();
    
    public function validateEmail(string $email): bool
    {
        //Check length
        if (mb_strlen($email) > self::EMAIL_MAX_LENGTH) {
            $this->errors[] = 'E-mail address mustn\'t be more than '.self::EMAIL_MAX_LENGTH.' characters long.';
            return false;
        }
        
        //Check format (might not allow some exotic but valid e-mail domains)
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
            $this->errors[] = 'E-mail address doesn\'t seem to be in the correct format. If you are sure that you entered your e-mail address properly, ping Shady#2948 on Discord.';
            return false;
        }
        
        //Check uniqueness
        $result = Db::fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE email = ? AND user_id != ?',
            array($email, $_SESSION['user']->getId()));
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
     * @param bool $checkAgainstOld TRUE, if the name should be also checked against the currently logged user's name
     * (default FALSE and TRUE should be used only when accounts are created by an admin, to permit changes in
     * capitalisation to causal users)
     * @return bool TRUE, if the name is valid
     * @throws \Exception
     */
    public function validateName(string $name, bool $checkAgainstOld = false): bool
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
        $result = Db::fetchQuery('SELECT COUNT(*) AS "cnt" FROM user WHERE UPPER(display_name) = ? AND user_id != ?',
            array(strtoupper($name), $checkAgainstOld ? 0 : $_SESSION['user']->getId()));
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
            $this->errors[] = 'An unknown error occurred during the file upload - try again or ping Shady#2948 on Discord.';
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
		
        $uppercaseBio = strtoupper($bio);
        $badwords = file('Models/BadWords.txt');
        foreach ($badwords as $badword) {
            if (mb_strpos($uppercaseBio, $badword) !== false) {
                $this->errors[] = 'Your bio contains a bad word: '.$badword.
                                  '. If you believe that it\'s not used as a profanity, ping Shady#2948 on Discord.';
                return false;
            }
        }
        
        return true;
    }
	
	public function sanitizeBio(string $bio): string
	{
		//Unify linebreaks
		$bio = str_replace('<br>', '<br />', $bio);
		
		//Run bio through tag and attribute whitelist
        $config = HTMLPurifier_Config::createDefault();
        $config->set('HTML.DefinitionID', 'enduser-customize.html tutorial');
		$config->set('HTML.DefinitionRev', 1);
		//$config->set('Cache.DefinitionImpl', null); // TODO: remove this later!

		$config->set('HTML.Allowed', 'p[style],span[style],strong,em,sup,sub,h1,h2,h3,a[title|href|target|rel],img[src|alt|width|height],br');
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
			$this->warnings[] = 'It seems like your bio contains disallowed HTML code. If you used only the tools provided in the toolbar and see unwanted changes, ping Shady#2948 on Discord please.';
		}
		return $result;
	}
}

