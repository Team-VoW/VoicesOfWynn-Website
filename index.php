<?php

namespace VoicesOfWynn;

use VoicesOfWynn\Controllers\Router;

//Set autoloader for dependencies
require __DIR__.'/vendor/autoload.php';

//Define and set autoloader for custom classes
function autoloader(string $name): void
{
    // Only autoload classes from the VoicesOfWynn namespace
    if (!str_starts_with($name, 'VoicesOfWynn\\')) {
        return;
    }
    //Replace '\' (used in namespaces) with '/' (used to navigate through directories)
    $name = str_replace('\\', '/', $name);
    //Remove the root folder from the path (this file is already in it)
    if (strpos($name, '/') !== false) {
        $name = substr($name, strpos($name, '/') + 1);
    }
    $name .= '.php';
    require $name;
}

spl_autoload_register('VoicesOfWynn\autoloader');

//Storage helper functions for views
function storageUrl(string $path, bool $cacheBust = false): string {
    return \VoicesOfWynn\Models\Storage\Storage::get()->getUrl($path, $cacheBust);
}

function storageBaseUrl(): string {
    return \VoicesOfWynn\Models\Storage\Storage::get()->getBaseUrl();
}

// Create "sessions" folder if it does not exist
$sessionsPath = __DIR__.'/sessions';
if (!is_dir($sessionsPath)) {
    mkdir($sessionsPath, 0777, true);
}

//Resume session and set character encoding
session_start();
mb_internal_encoding('UTF-8');

/* KEEP THIS COMMENTED ON LOCAL SERVERS - IT'S NOT POSSIBLE TO USE SSL ON THEM
//Check if HTTPS connection was used and if not (HTTP), redirect the client.
if (!(isset($_SERVER["HTTP_X_FORWARDED_PROTO"]) && $_SERVER["HTTP_X_FORWARDED_PROTO"] === "https") && substr($_SERVER['REQUEST_URI'], 0, 5 ) !== "/api/") {
    header('Location: https://'.$_SERVER['HTTP_HOST'].$_SERVER['REQUEST_URI']);
    header('Connection: close');
    exit();
}
*/

$requestedUrl = $_SERVER['REQUEST_URI'];
//Process the request
$router = new Router();
$result = $router->process(array($requestedUrl));

$website = '';
if ($result >= 400) {
    //Display the error webpage, overwrite the page headers (title, description, keywords)
    $errorControllerName = "VoicesOfWynn\Controllers\Errors\Error".$result;
    $errorController = new $errorControllerName();
    $errorController->process(array($router->isWebpageRequest));

    if ($router->isWebpageRequest) {
        $website = $errorController->getResult();
    } else {
        $website = $router->getResult();
    }
}
else if ($result === 204) {
    header('HTTP/1.1 204 No Content');
    //Don't render any views, simply don't echo anything into the response body
    //This is mostly used for AJAX calls
}
else if ($result < 300) {
    if ($result >= 100) {
        http_response_code($result);
    }
    $website = $router->getResult();
}

//Display the generated website
echo $website;

