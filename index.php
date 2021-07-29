<?php

namespace VoicesOfWynn;

use VoicesOfWynn\Controllers\Error404;
use VoicesOfWynn\Controllers\Rooter;

//Set autoloader for dependencies
require __DIR__.'/vendor/autoload.php';

//Define and set autoloader for custom classes
function autoloader(string $name): void
{
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

//Resume session and set character encoding
# session_start(); //Uncomment this line if sessions are going to be used (probably)
mb_internal_encoding('UTF-8');

/* KEEP THIS COMMENTED ON LOCAL SERVERS - IT'S NOT POSSIBLE TO USE SSL ON THEM
//Check if HTTPS connection was used and if not (HTTP), redirect the client.
if (!(isset($_SERVER["HTTP_X_FORWARDED_PROTO"]) && $_SERVER["HTTP_X_FORWARDED_PROTO"] === "https")) {
    header('Location: https://'.$_SERVER['HTTP_HOST'].$_SERVER['REQUEST_URI']);
    header('Connection: close');
    exit();
}
*/

$requestedUrl = $_SERVER['REQUEST_URI'];
//Process the request
$rooter = new Rooter();
$result = $rooter->process(array($requestedUrl));
if ($result !== true) {
    //Display the error webpage, overwrite the page headers (title, description, keywords)
    $errorController = new Error404();
    $errorController->process(array());
}

//Display the generated website
$website = $rooter->displayView();
echo $website;

