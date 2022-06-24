<?php


namespace VoicesOfWynn\Controllers;


use Exception;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Errors\ErrorController;
use VoicesOfWynn\Controllers\Website\WebpageController;

class Router extends Controller
{

    public bool $isWebpageRequest = true;
    private Controller $specificController;
	
	/**
	 * Method processing the request
	 * Decides which controller to call depending on the requested URL
	 * @param array $args Numeric array containing only one element - the requested URL
	 */
	public function process(array $args): int
	{
		$requestedUrl = strtok($args[0], '?'); //Remove the query string
		
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
			return 404;
		}
		$routeValue = $routes[$variableslessUrl];
		$arguments = explode('?', $routeValue); //Get the name of controller and the arguments (if they exist)
		
		//Replace variable placeholders with numeric variables
		for ($i = 0; $i < count($arguments); $i++) { //Index in $arguments
			if (preg_match('/^<\d>$/', $arguments[$i])) {
				$argNum = (int)substr($arguments[$i], 1, strlen($arguments[$i]) - 2);
				$arguments[$i] = $variables[$argNum];
			}
		}
		
		$controllerName = 'VoicesOfWynn\Controllers\\'.array_shift($arguments);
		$this->specificController = new $controllerName();
        if ($this->specificController instanceof WebpageController) {
            //Webpage request
            $this->isWebpageRequest = true;
        }
        else if ($this->specificController instanceof ApiController) {
            //Api request
            $this->isWebpageRequest = false;
        }
		return $this->specificController->process($arguments); //Pass control to the specific controller
    }

    public function getResult(): string {
        if ($this->isWebpageRequest === true) {
            //Webpage request
            return $this->specificController->displayView();
        }
        else {
            //Api request
            return $this->specificController->getOutput();
        }
    }
}

