<?php

namespace VoicesOfWynn\Controllers\Api;

use OpenApi\Generator;

require_once __DIR__ . '/../../vendor/autoload.php';

class Swagger extends ApiController{

    public function process(array $args): int
    {
        $generator = new Generator();
        $openapi = $generator->generate([
            __DIR__,  // Controllers/Api directory
            __DIR__ . '/../../Models'  // Models directory
        ]);
        echo $openapi->toJson();
        return 200;
    }
}
