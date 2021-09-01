<?php


namespace VoicesOfWynn\Models;


class User
{
    private const DEFAULT_PASSWORD_CHARACTERS = 'abcdefghijklmnopqrstuvwxyz0123456789';
    private const DEFAULT_PASSWORD_LENGTH = 6;
    
    private int $id = 0;
    private $email = '';
    private string $hash = '';
    private bool $systemAdmin = false;
    private string $displayName = '';
    private string $avatarLink = '';
    private $bio = '';
    private bool $publicEmail = false;
	
    private array $roles = array();
    
    /**
     * Registers a new user account, generates a password and returns it
     * The user is not logged in
     * @param string $name
     * @param string $checkAgainstOld If set to TRUE, users won't be able to pick names that are different from the
     * old ones only in capitalisation, default FALSE
     * @return string
     * @throws UserException In case of an invalid name
     */
    public function register(string $name, bool $checkAgainstOld = false)
    {
        $verifier = new AccountDataValidator();
        if (!$verifier->validateName($name, $checkAgainstOld)) {
            throw new UserException($verifier->errors[0]);
        }
        
        $password = '';
        for ($i = 0; $i < self::DEFAULT_PASSWORD_LENGTH; $i++) {
            $password .= self::DEFAULT_PASSWORD_CHARACTERS[rand(0, mb_strlen(self::DEFAULT_PASSWORD_CHARACTERS) - 1)];
        }
        
        $this->hash = password_hash($password, PASSWORD_DEFAULT);
        $result = Db::executeQuery('INSERT INTO user (display_name,password) VALUES (?,?)', array(
            $name,
            $this->hash
        ));
        
        if ($result) {
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
     * @return bool TRUE, if user is logged in successfully
     * @throws UserException In case of unknown username/email or incorrect password
     */
    public function login(string $name, string $password): bool
    {
        $userInfo = Db::fetchQuery('SELECT * FROM user WHERE email = ? OR display_name = ?', array($name, $name));
        if ($userInfo === false) {
            throw new UserException('The user with this name or e-mail doesn\'t exist');
        }
        
        $hash = $userInfo['password'];
        if (!password_verify($password, $hash)) {
            throw new UserException('Incorrect password');
        }
        
        $this->id = $userInfo['user_id'];
        $this->email = $userInfo['email'];
        $this->hash = $userInfo['password'];
        $this->systemAdmin = $userInfo['system_admin'];
        $this->displayName = $userInfo['display_name'];
        $this->avatarLink = $userInfo['picture'];
        $this->bio = $userInfo['bio'];
        $this->publicEmail = $userInfo['public_email'];
        
        $_SESSION['user'] = $this;
        
        return true;
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
        $this->publicEmail = false;
    }
    
    public function update(string $email, string $password, string $displayName, string $avatarLink, string $bio, bool $publicEmail): bool
    {
        if (empty($password)) {
            $parameters = array($email, $displayName, $avatarLink, $bio, $this->publicEmail, $this->id);
            $query = 'UPDATE user SET email = ?, display_name = ?, picture = ?, bio = ?, public_email = ? WHERE user_id = ?';
        } else {
            $hash = password_hash($password, PASSWORD_DEFAULT);
            $parameters = array($email, $hash, $displayName, $avatarLink, $bio, $this->publicEmail, $this->id);
            $query = 'UPDATE user SET email = ?, password = ?, display_name = ?, picture = ?, bio = ?, public_email = ? WHERE user_id = ?';
        }
        
        try {
            $result = Db::executeQuery($query, $parameters);
        } catch (\Exception $e) {
            return false;
        }
        
        $this->email = $email;
        if (isset($hash)) {
            $this->hash = $hash;
        }
        $this->displayName = $displayName;
        $this->avatarLink = $avatarLink;
        $this->bio = $bio;
        $this->publicEmail = $publicEmail;
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
     * E-mail getter
     * @return string E-mail address
     */
    public function getEmail()
    {
        return $this->email;
    }
    
    /**
     * Display name getter
     * @return string Display name
     */
    public function getName(): string
    {
        return $this->displayName;
    }
    
    /**
     * Avatar link getter
     * @return string Filename of the profile picture (a random number is appended to the end to prevent caching if
     * the avatar isn't the default one)
     */
    public function getAvatarLink(bool $appendRandom = true)
    {
    	if ($appendRandom && $this->avatarLink !== 'defaultAvatar.png') {
    		return $this->avatarLink.'?'.rand(0, 31);
	    }
        return $this->avatarLink;
    }
    
    /**
     * Bio getter
     * @return string User's bio
     */
    public function getBio()
    {
        return $this->bio;
    }
    
    /**
     * System admin getter
     * @return bool TRUE if this user is system admin
     */
    public function isSysAdmin(): bool
    {
        return $this->systemAdmin;
    }
	
	/**
	 * Private email getter
	 * @return bool TRUE if this user set his e-mail address as public
	 */
	public function hasPublicEmail(): bool
	{
		return $this->publicEmail;
	}
    
    /**
     * Method returning an array containing objects of type DiscordRole, representing all the roles that this user has
     * The returned array is also saved as an attribute of the object
     * @return array List of all the roles, each element being an associative array with keys "name" (string) and
     *     "color" (string, hex code of the color)
     */
    public function getRoles(): array
    {
        $result = Db::fetchQuery('SELECT name,color,weight FROM discord_role JOIN user_discord_role ON user_discord_role.discord_role_id = discord_role.discord_role_id WHERE user_id = ? ORDER BY weight DESC;',
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
            switch ($key) {
                case 'id':
	            case 'user_id':
	            case 'voice_actor':
	            case 'voice_actor_id':
                    $this->id = $value;
                    break;
                case 'email':
                    $this->email = $value;
                    break;
                case 'hash':
                    $this->hash = $value;
                    break;
                case 'systemAdmin':
                case 'admin':
                    $this->systemAdmin = $value;
                    break;
                case 'displayName':
                case 'display_name':
                case 'name':
                case 'uname':
                case 'vaname':
                case 'user_name':
                case 'voice_actor_name':
                    $this->displayName = $value;
                    break;
                case 'avatarLink':
                case 'avatar_link':
                case 'picture':
                    $this->avatarLink = $value;
                    break;
                case 'bio':
                case 'description':
                    $this->bio = $value;
                    break;
	            case 'public_email':
	            case 'publicEmail':
	            case 'has_public_email':
	            case 'hasPublicEmail':
					$this->publicEmail = $value;
            }
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
	 * @param int $roleId
	 * @return bool
	 * @throws \Exception
	 */
    public function addRole(int $roleId): bool
    {
    	return Db::executeQuery('INSERT INTO user_discord_role (user_id,discord_role_id) VALUES (?,?)', array(
    		$this->id,
    		$roleId
	    ));
    }
	
	/**
	 * Removes a role from this user in the database
	 * Doesn't affect this object's $roles attribute
	 * @param int $roleId
	 * @return bool
	 * @throws \Exception
	 */
	public function removeRole(int $roleId): bool
	{
		return Db::executeQuery('DELETE FROM user_discord_role WHERE user_id = ? AND discord_role_id = ?', array(
			$this->id,
			$roleId
		));
	}
	
	/**
	 * Removes bio of this user
	 * @return bool
	 * @throws \Exception
	 */
    public function clearBio(): bool
    {
    	$this->bio = '';
    	return Db::executeQuery('UPDATE user SET bio = NULL WHERE user_id = ?', array($this->id));
    }
	
	/**
	 * Resets this user's avatar to the default one
	 * @return bool
	 * @throws \Exception
	 */
	public function clearAvatar(): bool
	{
		$this->avatarLink = 'defaultAvatar.png';
		$result = Db::executeQuery('UPDATE user SET picture = DEFAULT WHERE user_id = ?', array($this->id));
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
		return Db::executeQuery('DELETE FROM user WHERE user_id = ?', array($this->id));
	}
}

