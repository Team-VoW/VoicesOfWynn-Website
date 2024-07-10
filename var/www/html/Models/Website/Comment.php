<?php

namespace VoicesOfWynn\Models\Website;

use VoicesOfWynn\Models\Db;

class Comment
{
	private int $id = 0;
    private bool $verified = false;
    private $userId = 0;
	private $ip = '0.0.0.0';
	private $name = '';
	private $email = '';
	private string $content = '';
	private int $recordingId = 0;
	
	private $gravatar = ''; //NULL for verified comments
	
	/**
	 * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
	 * each attribute
	 */
	public function __construct(array $data)
	{
		foreach ($data as $key => $value) {
			switch ($key) {
				case 'id':
				case 'comment_id':
					$this->id = $value;
					break;
                case 'verified':
                case 'logged':
                case 'registered':
                    $this->verified = $value;
                    break;
                case 'user_id':
                case 'userId':
                case 'user':
                case 'account':
                    $this->userId = $value;
                    break;
				case 'ip':
				case 'ip_address':
				case 'ipAddress':
				case 'ip_addr':
					$this->ip = $value;
				case 'name':
				case 'username':
				case 'author':
				case 'poster':
					$this->name = $value;
					break;
				case 'email':
				case 'e-mail':
				case 'emailAddress':
				case 'email-address':
					$this->email = $value;
					break;
				case 'content':
				case 'comment':
				case 'text':
				case 'message':
					$this->content = $value;
					break;
				case 'recording_id':
				case 'recordingId':
				case 'recording':
				case 'object':
					$this->recordingId = $value;
					break;
				case 'gravatar':
				case 'gravatar_link':
				case 'avatar':
				case 'avatar_link':
					$this->gravatar = $value;
					break;
			}
		}
	}
	
	/**
	 * Generic getter
	 * @param $attr
	 * @return mixed
	 */
	public function __get($attr)
	{
		if (isset($this->$attr)) {
			return $this->$attr;
		}
		return null;
	}
	
	/**
	 * Returns path the the avatar (either gravatar in case of unverified comments or profile picture in case of a verified ones).
	 * @return string URL to the profile picture (without domain name for the current server)
	 */
	public function getAvatar(): string
	{
		if ($this->verified) {
			$result = (new Db('Website/DbInfo.ini'))->fetchQuery("SELECT picture FROM user WHERE user_id = ?", array($this->userId));
			if ($result === false) { $result['picture'] = "default.png"; }
			return "http://".$_SERVER['SERVER_NAME']."/dynamic/avatars/".$result['picture']; //HTTP works most of the time
		}
		else {
			return "https://www.gravatar.com/avatar/".md5($this->email)."?d=identicon";
		}
	}
	
	/**
	 * Returns the name of the poster (either the chosen name of unverified comments or profile display name in case of a verified ones).
	 * @return string Name of the author of the comment
	 */
	public function getName(): string
	{
		if ($this->verified) {
			$result = (new Db('Website/DbInfo.ini'))->fetchQuery("SELECT display_name FROM user WHERE user_id = ?", array($this->userId));
			if ($result === false) { $result['display_name'] = "Deleted user"; }
			return $result['display_name'];
		}
		else {
			return $this->name;
		}
	}
}

