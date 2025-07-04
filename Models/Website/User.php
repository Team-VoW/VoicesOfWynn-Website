<?php


namespace VoicesOfWynn\Models\Website;


use InvalidArgumentException;
use JsonSerializable;
use PDOException;
use VoicesOfWynn\Controllers\Website\Account\Account;
use VoicesOfWynn\Models\Db;
use OpenApi\Attributes as OA;

#[OA\Schema(
    schema: "User",
    properties: [
        new OA\Property(property: "id", type: "integer", description: "User ID"),
        new OA\Property(property: "displayName", type: "string", description: "User's display name"),
        new OA\Property(property: "email", type: "string", nullable: true, description: "User's email address"),
        new OA\Property(property: "publicEmail", type: "boolean", description: "Whether email is public"),
        new OA\Property(property: "avatarLink", type: "string", description: "Link to user's avatar image"),
        new OA\Property(property: "bio", type: "string", nullable: true, description: "User's biography"),
        new OA\Property(property: "lore", type: "string", description: "User's lore/quote"),
        new OA\Property(property: "systemAdmin", type: "boolean", description: "Whether user is a system administrator"),
        new OA\Property(property: "discordId", type: "integer", nullable: true, description: "Discord user ID"),
        new OA\Property(property: "discordName", type: "string", nullable: true, description: "Discord username"),
        new OA\Property(property: "youtube", type: "string", nullable: true, description: "YouTube channel"),
        new OA\Property(property: "twitter", type: "string", nullable: true, description: "Twitter handle"),
        new OA\Property(property: "castingCallClub", type: "string", nullable: true, description: "Casting Call Club username"),
        new OA\Property(property: "roles", type: "array", items: new OA\Items(ref: "#/components/schemas/DiscordRole"), description: "User's Discord roles")
    ]
)]
class User implements JsonSerializable
{
    private const DEFAULT_PASSWORD_CHARACTERS = 'abcdefghijklmnopqrstuvwxyz0123456789';
    public const DEFAULT_PASSWORD_LENGTH = 12;
    private const LOG_PASSWORDS = false; //Turn this on when mass-creating user accounts, so you can message the temporary passwords to users all at once

    private bool $loaded = false;

    private int $id = 0;
    private $discordId = null;
    private $email = '';
    private string $hash = '';
    private bool $systemAdmin = false;
    private string $displayName = '';
    private string $avatarLink = '';
    private $bio = '';
    private $lore = '';
	private $discord = '';
	private $youtube = '';
	private $twitter = '';
	private $castingcallclub = '';
    private bool $publicEmail = false;
    
    private array $roles = array();

    public function jsonSerialize() : mixed
	{
	    return (object) [
            'id' => $this->id,
            'displayName' => $this->displayName,
            'email' => $this->email,
            'publicEmail' => $this->publicEmail,
            'avatarLink' => $this->getAvatarLink(),
            'bio' => $this->bio,
            'lore' => $this->lore,
            'systemAdmin' => $this->systemAdmin,
            'discordId' => $this->discordId,
            'discordName' => $this->discord,
            'youtube' => $this->youtube,
            'twitter' => $this->twitter,
            'castingCallClub' => $this->castingcallclub,
            'roles' => $this->getRoles()
        ];
	}

    /**
     * Function loading all user information from the database and saving them into attributes
     * @return void
     * @throws UserException If this object doesn't have its ID specified (necessary for the database search)
     */
    public function load(): void
    {
        if (empty($this->id)) {
            throw new UserException('The User object cannot be loaded, because it doesn\'t have its ID specified.');
        }
        $userInfo = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT * FROM user WHERE user_id = ?', array($this->id));

        $this->id = $userInfo['user_id'];
        $this->discordId = $userInfo['discord_id'];
        $this->email = $userInfo['email'];
        $this->hash = $userInfo['password'];
        $this->systemAdmin = $userInfo['system_admin'];
        $this->displayName = $userInfo['display_name'];
        $this->avatarLink = $userInfo['picture'];
        $this->bio = $userInfo['bio'];
        $this->discord = $userInfo['discord'];
        $this->youtube = $userInfo['youtube'];
        $this->twitter = $userInfo['twitter'];
        $this->castingcallclub = $userInfo['castingcallclub'];
        $this->publicEmail = $userInfo['public_email'];

        $this->loaded = true;
    }

    /**
     * Registers a new user account, generates a password and returns it
     * The user is not logged in
     * @param string $name
     * @oaran string $discordName Discord handle
     * @oaran string $discordName CCC username
     * @return string
     * @throws UserException In case of an invalid name
     */
    public function register(string $name, string $discordName = '', string $cccName = '')
    {
        $verifier = new AccountDataValidator();
        if (!$verifier->validateName($name, 0)) {
            throw new UserException($verifier->errors[0]);
        }

		if (!empty($discordName) && !$verifier->validateDiscord($discordName, 0)) {
			throw new UserException($verifier->errors[0]);
		}

        if (!empty($cccName) && !$verifier->validateCastingCallClub($cccName, 0)) {
            throw new UserException($verifier->errors[0]);
        }
        
        $password = $this->generateTempPassword();

        $this->hash = password_hash($password, PASSWORD_DEFAULT);
        $result = (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO user (display_name,password,discord,castingcallclub) VALUES (?,?,?,?)', array(
            $name,
            $this->hash,
	        $discordName,
            $cccName,
        ), true);
        
        if ($result) {
            if (self::LOG_PASSWORDS) {
                file_put_contents('profiles.php', $name.':'.$password.PHP_EOL, FILE_APPEND|LOCK_EX);
            }
            $this->id = $result;
            return $password;
        }
        else {
            throw new UserException('Couldn\'t execute the SQL query');
        }
    }

    /**
     * Registers a new user account with a set Discord user ID, generates a password and returns it
     * The user is not logged in
     * @param string $name String that will be used as a display name for the new user account
     * @param int $discordId Discord account ID for this account
     * @throws UserException
     */
    public function registerFromBot(string $name, int $discordId, string $discordName)
    {
        $verifier = new AccountDataValidator();
        if (!$verifier->validateName($name, 0)) {
            throw new UserException($verifier->errors[0]);
        }
        if (!$verifier->validateDiscord($discordName, 0)) {
            throw new UserException($verifier->errors[0]);
        }
        
        $password = $this->generateTempPassword();

        $this->hash = password_hash($password, PASSWORD_DEFAULT);
        $result = (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO user (display_name,password,discord_id,discord) VALUES (?,?,?,?)', array(
            $name,
            $this->hash,
            $discordId,
            $discordName
        ), true);
        
        if ($result) {
            if (self::LOG_PASSWORDS) {
                file_put_contents('profiles.php', $name.':'.$password.PHP_EOL, FILE_APPEND|LOCK_EX);
            }
            $this->id = $result;
            return $password;
        }
        else {
            throw new UserException('Couldn\'t execute the SQL query');
        }
    }

    /**
     * Login the user and load it's data, then save it's instance to the session
     * @param string $name Logging name or e-mail
     * @param string $password Logging Password
     * @return bool TRUE, if user is logged in successfully, FALSE if the password needs to be changed, or throws an exception in case of invalid credentials
     * @throws UserException In case of unknown username/email or incorrect password
     */
    public function login(string $name, string $password): bool
    {
        $userInfo = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT * FROM user WHERE email = ? OR display_name = ?', array($name, $name));
        if ($userInfo === false) {
            throw new UserException('The user with this name or e-mail doesn\'t exist');
        }
        
        $hash = $userInfo['password'];
        if (!password_verify($password, $hash)) {
            throw new UserException('Incorrect password');
        }
        
        if ($userInfo['force_password_change']) {
            return false;
        }

        $this->id = $userInfo['user_id'];
        $this->email = $userInfo['email'];
        $this->hash = $userInfo['password'];
        $this->systemAdmin = $userInfo['system_admin'];
        $this->displayName = $userInfo['display_name'];
        $this->avatarLink = $userInfo['picture'];
        $this->bio = $userInfo['bio'];
		$this->discord = $userInfo['discord'];
		$this->youtube = $userInfo['youtube'];
		$this->twitter = $userInfo['twitter'];
		$this->castingcallclub = $userInfo['castingcallclub'];
        $this->publicEmail = $userInfo['public_email'];
        
        $_SESSION['user'] = $this;

        $this->loaded = true;
        
        return true;
    }

    private function generateTempPassword(): string
    {
        $password = '';
        for ($i = 0; $i < self::DEFAULT_PASSWORD_LENGTH; $i++) {
            $password .= self::DEFAULT_PASSWORD_CHARACTERS[rand(0, mb_strlen(self::DEFAULT_PASSWORD_CHARACTERS) - 1)];
        }
        return $password;
    }

    public function changeTempPassword(string $name, string $newPassword): bool
    {
        if (!(
            isset($_COOKIE['passchangecode']) &&
            isset($_SESSION['passchangecode']) &&
            $_SESSION['passchangecode'] === $_COOKIE['passchangecode'])) {
            setcookie('passchangecode', 0, 0, '/');
            unset($_COOKIE['passchangecode']);
            unset($_SESSION['passchangecode']);
            unset($_SESSION['passchangename']);
            throw new UserException('The one-use password change token was not found or is incorrect. Please, login again using the temporary password.');
        }
        
        $validator = new AccountDataValidator();
        if (!$validator->validatePassword($newPassword)) {
            throw new UserException($validator->errors[0]);
        }
        $result = (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE user SET password = ?, force_password_change = 0 WHERE email = ? OR display_name = ?', array(
            password_hash($newPassword, PASSWORD_DEFAULT),
            $name,
            $name
        ));
        
        if ($result) {
            setcookie('passchangecode', 0, 0, '/');
            unset($_COOKIE['passchangecode']);
            unset($_SESSION['passchangecode']);
            unset($_SESSION['passchangename']);
            return $this->login($name, $newPassword);
        }
        throw new UserException('An error occurred. Please, try again later and if the error persists, ping shady_medic on Discord.');
    }
    
    /**
     * Logout the user by deleting its instance from the session and setting all of its properties to zero values
     * This instance should be destroyed with unset() after calling this method
     */
    public function logout(): void
    {
        unset($_SESSION['user']);
        
        $this->id = 0;
        $this->email = '';
        $this->hash = '';
        $this->systemAdmin = false;
        $this->displayName = '';
        $this->avatarLink = '';
        $this->bio = '';
        $this->lore = '';
	    $this->discord = '';
	    $this->youtube = '';
	    $this->twitter = '';
	    $this->castingcallclub = '';
        $this->publicEmail = false;

        $this->loaded = false;
    }
    
    public function update($email, string $password, string $displayName, string $avatarLink, $bio, $discord, $youtube, $twitter, $castingcallclub, bool $publicEmail): bool
    {
        if (empty($password)) {
            $parameters = array($email, $displayName, $avatarLink, $bio, $discord, $youtube, $twitter, $castingcallclub, $publicEmail, $this->id);
            $query = 'UPDATE user SET email = ?, display_name = ?, picture = ?, bio = ?, discord = ?, youtube = ?, twitter = ?, castingcallclub = ?, public_email = ? WHERE user_id = ?';
        } else {
            $hash = password_hash($password, PASSWORD_DEFAULT);
            $parameters = array($email, $hash, $displayName, $avatarLink, $bio, $discord, $youtube, $twitter, $castingcallclub, $publicEmail, $this->id);
            $query = 'UPDATE user SET email = ?, password = ?, display_name = ?, picture = ?, bio = ?, discord = ?, youtube = ?, twitter = ?, castingcallclub = ?, public_email = ? WHERE user_id = ?';
        }
        
        try {
            $result = (new Db('Website/DbInfo.ini'))->executeQuery($query, $parameters);
        } catch (PDOException $e) {
            return false;
        }
        
        $this->email = $email;
        if (isset($hash)) {
            $this->hash = $hash;
        }
        $this->displayName = $displayName;
        $this->avatarLink = $avatarLink;
        $this->bio = $bio;
		$this->discord = $discord;
		$this->youtube = $youtube;
		$this->twitter = $twitter;
		$this->castingcallclub = $castingcallclub;
        $this->publicEmail = $publicEmail;

        $this->loaded = true;
        return $result;
    }
    
    /**
     * User ID getter
     * @return int User ID
     */
    public function getId(): int
    {
        return $this->id;
    }

    /**
     * Summary of getDiscordId
     * @return int|null Discord ID or NULL, if it has not been specified
     */
    public function getDiscordId(): ?int
    {
        return $this->discordId;
    }
    
    /**
     * E-mail getter
     * @return string E-mail address
     */
    public function getEmail()
    {
        if (!$this->loaded && empty($this->email)) {
            $this->load();
        }
        return $this->email;
    }
    
    /**
     * Display name getter
     * @return string Display name
     */
    public function getName(): string
    {
        if (!$this->loaded && empty($this->displayName)) {
            $this->load();
        }
        return $this->displayName;
    }
    
    /**
     * Method returning filename of the avatar image without any additional path to it
     * This should only ever be used by anti-XSS sanitizer to not duplicate the path part of the full filename
     * @return string Filename of the avatar of this user
     */
    public function getAvatar(): string
    {
        if (!$this->loaded && empty($this->avatarLink)) {
            $this->load();
        }
        return $this->avatarLink;
    }
	
    /**
     * Method returning link of avatar image that should be displayed.
     * By defualt, the avatar set manually by the user in their account settings is used. If none has been set, the one
     * fetched from Discord (downloaded during account creation via API) is returned (if such avatar exists).
     * If none of the avatars mentioned above is present, the default (empty) one is returned.
     * @param bool $appendRandom Should a random number in range 0–31 be appended to the file name, to prevent caching?
     * @return string Link of the profile picture (a random number is appended to the end to prevent caching if
     * the avatar isn't the default one) and the argument $appendRandom is set to TRUE.
     */
    public function getAvatarLink(bool $appendRandom = true)
    {
        if (!$this->loaded && empty($this->avatarLink)) {
            $this->load();
        }
        if ($appendRandom && $this->avatarLink !== 'default.png') {
            return Account::PROFILE_AVATAR_DIRECTORY.$this->avatarLink.'?'.rand(0, 31);
        }
        if (!$appendRandom && $this->avatarLink !== 'default.png') {
            return Account::PROFILE_AVATAR_DIRECTORY.$this->avatarLink;
        }
        if (file_exists(Account::DISCORD_AVATAR_DIRECTORY.$this->id.'.png')) {
            return Account::DISCORD_AVATAR_DIRECTORY . $this->id . '.png';
        }
        return Account::PROFILE_AVATAR_DIRECTORY.$this->avatarLink;
    }
    
    /**
     * Bio getter
     * @return string User's bio
     */
    public function getBio()
    {
        if (!$this->loaded && empty($this->bio)) {
            $this->load();
        }
        return $this->bio;
    }
    
    /**
     * Lore getter
     * @return string User's bio
     */
    public function getLore()
    {
        if (!$this->loaded && empty($this->lore)) {
            $this->load();
        }
        return $this->lore;
    }
    
	public function getSocial(string $network)
	{
		$network = strtolower($network);
		switch ($network) {
			case 'discord':
                if (!$this->loaded && empty($this->discord)) {
                    $this->load();
                }
				return $this->discord;
			case 'youtube':
			case 'yt':
                if (!$this->loaded && empty($this->youtube)) {
                    $this->load();
                }
				return $this->youtube;
			case 'twitter':
                if (!$this->loaded && empty($this->twitter)) {
                    $this->load();
                }
				return $this->twitter;
			case 'castingcallclub':
			case 'casting_call_club':
			case 'ccc':
            if (!$this->loaded && empty($this->castingcallclub)) {
                $this->load();
            }
				return $this->castingcallclub;
			default:
				return false;
		}
	}
	
    /**
     * System admin getter
     * @return bool TRUE if this user is system admin
     */
    public function isSysAdmin(): bool
    {
        if (!$this->loaded) {
            $this->load();
        }
        return $this->systemAdmin;
    }
    
    /**
     * Private email getter
     * @return bool TRUE if this user set his e-mail address as public
     */
    public function hasPublicEmail(): bool
    {
        if (!$this->loaded) {
            $this->load();
        }
        return $this->publicEmail;
    }
    
    /**
     * Method returning an array containing objects of type DiscordRole, representing all the roles that this user has
     * The returned array is also saved as an attribute of the object
     * In case the $roles attribute is not empty, it's returned and a database query is not executed
     * @return DiscordRole[] List of all the roles, each element being an associative array with keys "name" (string) and
     *     "color" (string, hex code of the color)
     */
    public function getRoles(): array
    {
        if (!empty($this->roles)) {
            return $this->roles;
        }

        $result = (new Db('Website/DbInfo.ini'))->fetchQuery('SELECT `name`,`color`,`weight` FROM discord_role JOIN user_discord_role ON user_discord_role.discord_role_id = discord_role.discord_role_id WHERE user_id = ? ORDER BY weight DESC;',
            array($this->id), true);
        if ($result === false) {
            $this->roles = array();
            return array();
        }
        
        $answer = array();
        foreach ($result as $role) {
            $role = new DiscordRole($role['name'], $role['color'], $role['weight']);
            $answer[] = $role;
        }
        
        $this->roles = $answer;
        return $answer;
    }
    
    /**
     * Finds out if the user has a certain discord role by its name
     * If the list of roles haven't been loaded yet, it'll be after calling this function
     * @param string $roleName Name of the role (case sensitive)
     * @return bool TRUE, if this user has the role, FALSE if it doesn't
     */
    public function hasRole(string $roleName): bool
    {
        if (empty($this->roles)) {
            $this->getRoles();
        } //Load roles if they are not loaded
        foreach ($this->roles as $role) {
            if ($role->name === $roleName) {
                return true;
            }
        }
        return false;
    }
    
    /**
     * Generic setter for all properties
     * @param array $data Associative array containing values to set. There are multiple allowed key names for each
     * attribute and any of the attributes can be omitted
     */
    public function setData(array $data): void
    {
        foreach ($data as $key => $value) {
			$key = strtolower($key);
            switch ($key) {
                case 'id':
                case 'user_id':
                case 'voice_actor':
                case 'voice_actor_id':
                    $this->id = $value;
                    break;
                case 'discordId':
                case 'discord_id':
                case 'dId':
                    $this->discordId = $value;
                    break;
                case 'email':
                    $this->email = $value;
                    break;
                case 'hash':
                    $this->hash = $value;
                    break;
                case 'systemadmin':
                case 'admin':
                    $this->systemAdmin = $value;
                    break;
                case 'displayname':
                case 'display_name':
                case 'name':
                case 'uname':
                case 'vaname':
                case 'user_name':
                case 'voice_actor_name':
                    $this->displayName = $value;
                    break;
                case 'avatarlink':
                case 'avatar_link':
                case 'avatar':
                case 'picture':
                    $this->avatarLink = $value;
                    break;
                case 'bio':
                case 'description':
                    $this->bio = $value;
                    break;
                case 'lore':
                case 'quote':
                    $this->lore = $value;
                    break;
                case 'discord':
                case 'discord_name':
                case 'discordName':
                    $this->discord = $value;
                    break;
                case 'youtube':
                case 'yt':
                    $this->youtube = $value;
                    break;
                case 'twitter':
                    $this->twitter = $value;
                    break;
                case 'castingcallclub':
                case 'casting_call_club':
                case 'ccc':
                    $this->castingcallclub = $value;
                    break;
                case 'public_email':
                case 'publicemail':
                case 'has_public_email':
                case 'haspublicemail':
                    $this->publicEmail = $value;
                    break;
            }
        }
    }

    public function saveDiscordInfo(): bool
    {
        if (!empty($this->id)) {
            return (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE user SET discord_id = ?, discord = ? WHERE user_id = ?', array(
                $this->discordId,
                $this->discord,
                $this->id
            ));
        } else {
            throw new UserException('Discord info of this user object cannot be saved, because its ID isn\'t known.');
        }
    }
    
    /**
     * Setter for the $roles attribute
     * @param array $roles Array of the DiscordRole objects
     */
    public function setRoles(array $roles)
    {
        $this->roles = $roles;
    }
    
    /**
     * Adds a role to this user in the database
     * Doesn't affect this object's $roles attribute
     * Both arguments are optional, but at least one needs to be specified
     * @param int|null $roleId Role ID to add, if specified, the second argument is ignored
     * @param DiscordRole|null $role Role object to add, must have its name filled in, the other attributes aren't important
     * @return bool
     * @throws InvalidArgumentException If none of the arguments are specified
     */
    public function addRole(?int $roleId = null, DiscordRole $role = null): bool
    {
        if (!is_null($roleId)) {
            return (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO user_discord_role (user_id,discord_role_id) VALUES (?,?)', array(
                $this->id,
                $roleId
            ));
        }

        if (is_null($role)) {
            throw new InvalidArgumentException("At least one argument needs to be specified for the User::addRole() method.");
        }
        return (new Db('Website/DbInfo.ini'))->executeQuery('INSERT INTO user_discord_role (user_id,discord_role_id) VALUES (?,(SELECT discord_role_id FROM discord_role WHERE name = ? LIMIT 1))', array(
            $this->id,
            $role->name
        ));
    }

    /**
     * Removes a role from this user in the database
     * Doesn't affect this object's $roles attribute
     * Both arguments are optional, but at least one needs to be specified
     * @param int|null $roleId Role ID to remove, if specified, the second argument is ignored
     * @param DiscordRole|null $role Role object to remove, must have its name filled in, the other attributes aren't important
     * @return bool
     * @throws InvalidArgumentException If none of the arguments are specified
     */
    public function removeRole(?int $roleId = null, DiscordRole $role = null): bool
    {
        if (!is_null($roleId)) {
            return (new Db('Website/DbInfo.ini'))->executeQuery('DELETE FROM user_discord_role WHERE user_id = ? AND discord_role_id = ?', array(
                $this->id,
                $roleId
            ));
        }

        if (is_null($role)) {
            throw new InvalidArgumentException("At least one argument needs to be specified for the User::removeRole() method.");
        }
        return (new Db('Website/DbInfo.ini'))->executeQuery('DELETE FROM user_discord_role WHERE user_id = ? AND discord_role_id = (SELECT discord_role_id FROM discord_role WHERE name = ? LIMIT 1)', array(
            $this->id,
            $role->name
        ));
    }

    /**
     * Replaces all the current Discord roles of this user with the new ones.
     * Roles that persist aren't removed from the databases, diff between the current and the new roles is computed
     * so as few SQL queries need to be executed as possible.
     * @param DiscordRole[] $newRoles List of all the Discord roles the user should have
     * @return bool
     */
    public function updateRoles(array $newRoles): bool
    {
        $currentRoles = $this->getRoles();

        //Compare the role arrays by role names
        $compareFunction = function(DiscordRole $r1, DiscordRole $r2) {
            if ($r1->name === $r2->name) {
                return 0;
            } else if ($r1->name < $r2->name) {
                return -1;
            } else {
                return 1;
            }
        };

        $addedRoles = array_udiff($newRoles, $currentRoles, $compareFunction);
        $removedRoles = array_udiff($currentRoles, $newRoles, $compareFunction);

        foreach ($addedRoles as $role) {
            $this->addRole(null, $role);
        }

        foreach ($removedRoles as $role) {
            $this->removeRole(null, $role);
        }

        return true;
    }

    /**
     * Resets the password and replaces it with a new temporary, randomly generated one
     * @param bool $allowForSysadmins Should this functions be able to reset the password of system administrators
     * @return string The new password
     * @throws \Exception
     */
    public function resetPassword(bool $allowForSysadmins = false): string
    {
        if (!$allowForSysadmins) {
            if ($this->isSysAdmin()) {
                throw new UserException("Password of a system administrator cannot be reset this way for security reasons. Contact shady_medic on Discord for assistance.");
            }
        }

        $newPassword = $this->generateTempPassword();
        $db = new Db('Website/DbInfo.ini');
        $this->hash = password_hash($newPassword, PASSWORD_DEFAULT);
        $db->executeQuery('UPDATE user SET password = ?, force_password_change = 1 WHERE user_id = ? LIMIT 1', array(
            $this->hash,
            $this->id
        ));
        return $newPassword;
    }

    /**
     * Removes bio of this user
     * @return bool
     * @throws \Exception
     */
    public function clearBio(): bool
    {
        $this->bio = '';
        return (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE user SET bio = NULL WHERE user_id = ?', array($this->id));
    }
    
    /**
     * Resets this user's avatar to the default one
     * @return bool
     * @throws \Exception
     */
    public function clearAvatar(): bool
    {
        $this->avatarLink = 'default.png';
        $result = (new Db('Website/DbInfo.ini'))->executeQuery('UPDATE user SET picture = DEFAULT WHERE user_id = ?', array($this->id));
        if ($result) {
            array_map('unlink', glob('dynamic/avatars/'.$this->getId().'.*'));
        }
        return $result;
    }
    
    /**
     * Deletes this user from the database
     * This object should be immediately destroyed with unset()
     * @return bool
     * @throws \Exception
     */
    public function delete(): bool {
        return (new Db('Website/DbInfo.ini'))->executeQuery('DELETE FROM user WHERE user_id = ?', array($this->id));
    }
}

