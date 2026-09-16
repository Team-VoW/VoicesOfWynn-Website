<?php

namespace VoicesOfWynn\Models\Api\FunFacts;

use PDOException;
use VoicesOfWynn\Models\Db;

class FunFactGenerator
{

    /**
     * Loads and returns a random active fun fact.
     * The library used to be a directory of .txt files globbed on every mod boot; it now lives in
     * the api database (fun_fact) so it can be edited from the staff Admin page.
     * @return string A randomly picked fun fact, or an empty string if there are none
     */
    public function getRandomFact(): string
    {
        try {
            $result = (new Db('Api/FunFacts/DbInfo.ini'))->fetchQuery(
                'SELECT content FROM fun_fact WHERE active = 1 ORDER BY RAND() LIMIT 1'
            );
        } catch (PDOException $e) {
            error_log('Loading a fun fact failed: '.$e->getMessage());
            return '';
        }

        return ($result) ? $result['content'] : '';
    }
}
