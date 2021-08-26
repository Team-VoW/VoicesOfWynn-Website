<?php


namespace VoicesOfWynn\Controllers;


use Exception;
use VoicesOfWynn\Models\Comment;
use VoicesOfWynn\Models\DiscordRole;
use VoicesOfWynn\Models\Npc;
use VoicesOfWynn\Models\Quest;
use VoicesOfWynn\Models\Recording;
use VoicesOfWynn\Models\User;

class Router extends Controller
{
	
	private const VIEWS_FOLDER = 'Views';
	
	public int $errorCode = 0;
	
	/**
	 * Method processing the request
	 * Decides which controller to call depending on the requested URL
	 * @param array $args Numeric array containing only one element - the requested URL
	 */
	public function process(array $args): bool
	{
		$requestedUrl = $args[0];
		
		//Separate variables from the URL
		$urlArguments = explode('/', $requestedUrl);
		$variableslessUrl = '';
		$variables = array();
		foreach ($urlArguments as $urlArgument) {
			if (!is_numeric($urlArgument)) {
				$variableslessUrl .= $urlArgument.'/';
			} else {
				$variableslessUrl .= '<'.count($variables).'>/';
				$variables[] = $urlArgument;
			}
		}
		$variableslessUrl = rtrim($variableslessUrl, '/'); //Remove trailing slash
		if (strlen($variableslessUrl) === 0) {
			$variableslessUrl = '/'; //Set to index if nothing was left
		}
		
		//Find out which controller to call
		$routes = parse_ini_file('routes.ini');
		if (!isset($routes[$variableslessUrl])) {
			$this->errorCode = 404;
			return false;
		}
		$routeValue = $routes[$variableslessUrl];
		$arguments = explode('?', $routeValue); //Get the name of controller and the arguments (if they exist)
		
		//Replace variable placeholders with numeric variables
		for ($i = 0; $i < count($arguments); $i++) { //Index in $arguments
			if (preg_match('/^<\d>$/', $arguments[$i])) {
				$argNum = (int)substr($arguments[$i], 1, strlen($arguments[$i]) - 2);
				$arguments[$i] = $variables[$argNum];
				$i++;
			}
		}
		
		$controllerName = 'VoicesOfWynn\Controllers\\'.array_shift($arguments);
		$controller = new $controllerName();
		return $controller->process($arguments); //Pass control to the specific controller
	}
	
	/**
	 * Method composing the final website from the list of views and data supplied by specific controllers
	 * @return string Final website to send to the user
	 */
	public function displayView(): string
	{
		//Sanitize against XSS attack
		$sanitized = array();
		foreach (self::$data as $key => $value) {
			$sanitized[$key] = $this->sanitize($value);
		}
		extract($sanitized);
		
		ob_start();
		require $this->getNextView();
		$result = ob_get_contents();
		ob_end_clean();
		return $result;
	}
	
	/**
	 * Method sanitizing one variable of type int, double, string or array against XSS attack
	 * @param $value mixed Variable to sanitize
	 * @return mixed Sanitized variable of the same type
	 */
	private function sanitize($value)
	{
		$return = null;
		switch (gettype($value)) {
			case 'NULL':
				$return = null;
				break;
			case 'boolean':
				$return = (bool)$value;
				break;
			case 'string':
			case 'double':
			case 'integer':
				$return = htmlspecialchars($value, ENT_QUOTES);
				break;
			case 'array':
				$return = array();
				foreach ($value as $key => $val) {
					$return[$key] = $this->sanitize($val);
				}
				break;
			case 'object':
				if ($value instanceof DiscordRole) {
					$return = new DiscordRole("TempName");
					$return->name = $this->sanitize($value->name);
					$return->color = $this->sanitize($value->color);
					$return->weight = $this->sanitize($value->weight);
				}
				else if ($value instanceof User) {
					$id = $this->sanitize($value->getId());
					$email = $this->sanitize($value->getEmail());
					$name = $this->sanitize($value->getName());
					$avatarLink = $this->sanitize($value->getAvatarLink(false));
					$bio = $this->sanitize($value->getBio());
					$roles = $this->sanitize($value->getRoles());
					
					$value->setData(array(
						'id' => $id,
						'email' => $email,
						'displayName' => $name,
						'avatarLink' => $avatarLink,
						'bio' => $bio
					));
					$value->setRoles($roles);
					
					$return = $value;
				}
				else if ($value instanceof Quest) {
					$id = $this->sanitize($value->getId());
					$name = $this->sanitize($value->getName());
					$quest = new Quest(array('id' => $id, 'name' => $name));
					foreach ($value->getNpcs() as $npc) {
						$quest->addNpc($this->sanitize($npc));
					}
					$return = $quest;
				}
				else if ($value instanceof Npc) {
					$attr = array();
					$attr['id'] = $this->sanitize($value->getId());
					$attr['name'] = $this->sanitize($value->getName());
					$voiceActor = $this->sanitize($value->getVoiceActor());
					$npc = new Npc($attr);
					if ($voiceActor !== null) {
						$npc->setVoiceActor($voiceActor);
					}
					foreach ($value->getRecordings() as $recording) {
						$npc->addRecording($this->sanitize($recording));
					}
					$return = $npc;
				}
				else if ($value instanceof Recording) {
					$return = $value;
					$attr = array();
					$attr['id'] = $this->sanitize($value->id);
					$attr['npc_id'] = $this->sanitize($value->npcId);
					$attr['quest_id'] = $this->sanitize($value->questId);
					$attr['line'] = $this->sanitize($value->line);
					$attr['file'] = $this->sanitize($value->file);
					$attr['upvotes'] = $this->sanitize($value->upvotes);
					$attr['downvotes'] = $this->sanitize($value->downvotes);
					$attr['comments'] = $this->sanitize($value->comments);
					return new Recording($attr);
				}
				else if ($value instanceof Comment) {
					$attr = array();
					$attr['id'] = $this->sanitize($value->id);
					$attr['name'] = $this->sanitize($value->name);
					$attr['email'] = $this->sanitize($value->email);
					$attr['content'] = $this->sanitize($value->content);
					$attr['recording_id'] = $this->sanitize($value->recordingId);
					return new Comment($attr);
				}
				else {
					throw new Exception('Object variable of class '.get_class($value).' couldn\'t be sanitized');
				}
				break;
			default:
				throw new Exception('Variable of type '.gettype($value).' couldn\'t be sanitized');
		}
		return $return;
	}
	
	/**
	 * Method returning a path to the next (more inner) view to display
	 * @return string Path to the view
	 */
	public function getNextView(): string
	{
		return self::VIEWS_FOLDER.'/'.array_shift(self::$views).'.phtml';
	}
	
	/**
	 * Method finding and returning a variable to be inserted to a view by its name
	 * @param $variableName
	 * @return mixed
	 */
	public static function ins($variableName)
	{
		echo __FILE__;
		$viewName = basename(__FILE__, '.phtml');
		$viewName = str_replace('-', '', $viewName);
		return ${$viewName.'_'.$variableName};
	}
}

