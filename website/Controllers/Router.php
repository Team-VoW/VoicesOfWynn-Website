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
		$requestedUrl = strtok($args[0], '?');          //Remove the query string
        $requestedUrl = trim($requestedUrl, '/');   //Remove starting and trailing slash
		
		//Separate variables from the URL
        $ini = parse_ini_file('routes.ini', true);
        $routes = $ini['Routes'];
        $keywords = array_keys($ini['Keywords']);

		$urlArguments = empty($requestedUrl) ? [] : explode('/', $requestedUrl);
        $variables = array_diff($urlArguments, $keywords);
        $urlVariablesPositions = array_keys($variables);
        $urlVariablesValues = array_values($variables);

        for ($i = 0, $j = 0; $i < count($urlArguments); $i++) {
            if (in_array($i, $urlVariablesPositions)) {
                $urlArguments[$i] = '<'.$j.'>';
                $j++; //Variable number
            }
        }
        $variableslessUrl = '/'.implode('/', $urlArguments);
		$variableslessUrl = rtrim($variableslessUrl, '/'); //Remove trailing slash
		if (strlen($variableslessUrl) === 0) {
			$variableslessUrl = '/'; //Set to index if nothing was left
		}
		
		//Find out which controller to call
		if (!isset($routes[$variableslessUrl])) {
			return 404;
		}
		$routeValue = $routes[$variableslessUrl];
		$arguments = explode('?', $routeValue); //Get the name of controller and the arguments (if they exist)
		
		//Replace variable placeholders with numeric variables
		for ($i = 0; $i < count($arguments); $i++) { //Index in $arguments
			if (preg_match('/^<\d>$/', $arguments[$i])) {
				$argNum = (int)substr($arguments[$i], 1, strlen($arguments[$i]) - 2);
				$arguments[$i] = $urlVariablesValues[$argNum];
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

