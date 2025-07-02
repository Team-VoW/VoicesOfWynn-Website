<?php

namespace VoicesOfWynn\Controllers\Api;


use OpenApi\Generator;
use Doctrine\Common\Annotations\AnnotationReader;
require_once __DIR__ . '/../../vendor/autoload.php';
class Swagger extends ApiController{

    public function process(array $args): int
    {
        // Ignore standard PHPDoc tags to prevent Doctrine from trying to autoload them
        if (class_exists(AnnotationReader::class)) {
            AnnotationReader::addGlobalIgnoredName('see');
            AnnotationReader::addGlobalIgnoredName('noinspection');
            AnnotationReader::addGlobalIgnoredName('var');
            AnnotationReader::addGlobalIgnoredName('return');
            AnnotationReader::addGlobalIgnoredName('param');
            AnnotationReader::addGlobalIgnoredName('method');
            AnnotationReader::addGlobalIgnoredName('property');
            AnnotationReader::addGlobalIgnoredName('property-read');
            AnnotationReader::addGlobalIgnoredName('property-write');
        }
        $generator = new Generator();
        $openapi = $generator->generate([__DIR__]);
        echo $openapi->toJson();
        return 200;
    }
}
