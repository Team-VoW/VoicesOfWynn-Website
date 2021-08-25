<?php

namespace VoicesOfWynn\Models;

class Recording
{
	private int $id = 0;
	private int $npcId = 0;
	private int $questId = 0;
	private int $line = 0;
	private string $file = '';
	private int $upvotes = 0;
	private int $downvotes = 0;
	private int $comments = 0;
	
	/**
	 * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
	 * each attribute
	 */
	public function __construct(array $data)
	{
		foreach ($data as $key => $value) {
			switch ($key) {
				case 'id':
				case 'recording_id':
					$this->id = $value;
					break;
				case 'npcId':
				case 'npc_id':
				case 'npc':
					$this->npcId = $value;
					break;
				case 'questId':
				case 'quest_id':
				case 'quest':
					$this->questId = $value;
					break;
				case 'line':
				case 'number':
				case 'line_number':
					$this->line = $value;
					break;
				case 'file':
				case 'filename':
				case 'fileName':
				case 'recording':
				case 'audio':
					$this->file = $value;
					break;
				case 'upvotes':
				case 'likes':
					$this->upvotes = $value;
					break;
				case 'downvotes':
				case 'dislikes':
					$this->downvotes = $value;
					break;
				case 'comments':
				case 'comment_count':
				case 'commentCount':
					$this->comments = $value;
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
	 * Upvotes this recording and sets the cookie preventing duplicate votes
	 * @return bool
	 * @throws \Exception
	 */
	public function upvote(): bool
	{
		setcookie('votedFor'.$this->id, 1, time() + 31536000, '/');
		return Db::executeQuery('UPDATE recording SET upvotes = upvotes + 1 WHERE recording_id = ?;', array($this->id));
	}
	
	/**
	 * Downvotes this recording and sets the cookie preventing duplicate votes
	 * @return bool
	 * @throws \Exception
	 */
	public function downvote(): bool
	{
		setcookie('votedFor'.$this->id, 1, time() + 31536000, '/');
		return Db::executeQuery('UPDATE recording SET downvotes = downvotes + 1 WHERE recording_id = ?;',
			array($this->id));
	}
	
	/**
	 * Adds a new comment to this recording
	 * @param $author
	 * @param $email
	 * @param $content
	 * @return bool
	 * @throws \Exception
	 */
	public function comment($author, $email, $content)
	{
		return Db::executeQuery('INSERT INTO comment (name,email,content,recording_id) VALUES (?,?,?,?);', array(
			$author,
			$email,
			$content,
			$this->id
		));
	}
}

