<?php

namespace VoicesOfWynn;

//Set autoloader for dependencies
require __DIR__.'/vendor/autoload.php';

//Define and set autoloader for custom classes
function autoloader(string $name): void
{
    //Replace '\' (used in namespacves) with '/' (used to navigate through directories)
    $name = str_replace('\\', '/', $name);
    //Remove the root folder from the path (this file is already in it)
    if (strpos($name, '/') !== false) {
        $name = substr($name, strpos($name, '/'));
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
    (new Logger(true))->notice('Uživatel se pokusil odeslat požadavek na adresu {uri} z IP adresy {ip}, avšak nepoužil zabezpečené SSL připojení',
        array('uri' => $_SERVER['REQUEST_URI'], 'ip' => $_SERVER['REMOTE_ADDR']));
    header('Location: https://'.$_SERVER['HTTP_HOST'].$_SERVER['REQUEST_URI']);
    header('Connection: close');
    exit();
}
*/

$requestedUrl = $_SERVER['REQUEST_URI'];
//TODO - process the request
echo 'Requested URL: '.$requestedUrl;

//TODO - display the generated website
