<?php

namespace VoicesOfWynn\Models;

class Comment
{
	private int $id = 0;
	private string $name = '';
	private string $email = '';
	private string $content = '';
	private int $recordingId = 0;
	
	private string $gravatar = '';
	
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
}

