<?php


namespace VoicesOfWynn\Models;


class User
{
    private int $id;
    private string $email;
    private string $hash;
    private bool $systemAdmin;
    private string $displayName;
    private string $avatarLink;
    private string $bio;
    
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
    }
    
    public function update(string $email, string $password, string $displayName, string $avatarLink, string $bio): bool
    {
        if (empty($password)) {
            $parameters = array($email, $displayName, $avatarLink, $bio, $this->id);
            $query = 'UPDATE user SET email = ?, display_name = ?, picture = ?, bio = ? WHERE user_id = ?';
        } else {
            $hash = password_hash($password, PASSWORD_DEFAULT);
            $parameters = array($email, $hash, $displayName, $avatarLink, $bio, $this->id);
            $query = 'UPDATE user SET email = ?, password = ?, display_name = ?, picture = ?, bio = ? WHERE user_id = ?';
        }
        
        try {
            $result = Db::executeQuery($query, $parameters);
        } catch (\Exception $e) {
            return false;
        }
        
        $this->email = $email;
        if (isset($hash)) { $this->hash = $hash; }
        $this->displayName = $displayName;
        $this->avatarLink = $avatarLink;
        $this->bio = $bio;
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
    public function getEmail(): string
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
     * @return string Filename of the profile picture
     */
    public function getAvatarLink(): string
    {
        return $this->avatarLink;
    }
    
    /**
     * Bio getter
     * @return string User's bio
     */
    public function getBio(): string
    {
        return $this->bio;
    }
}