<?php


namespace VoicesOfWynn\Models;


use Exception;
use PDO;
use PDOException;

class Db
{
    private const DB_HOST = 'localhost';
    private const DB_USER = 'root';
    private const DB_PASSWORD = '';
    private const DB_NAME = 'voices-of-wynn';
    private static array $settings = array(
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::MYSQL_ATTR_INIT_COMMAND => "SET NAMES utf8",
        PDO::ATTR_EMULATE_PREPARES => false
    );
    
    private static PDO $connection;
    
    /**
     * Database wrapper method connecting to the database.
     */
    private static function connect()
    {
        try {
            self::$connection = new PDO('mysql:host='.self::DB_HOST.';dbname='.self::DB_NAME, self::DB_USER,
                self::DB_PASSWORD, self::$settings);
        } catch (PDOException $e) {
            return false;
        }
        return true;
    }
    
    /**
     * Execute a query that doesn't return any data (suitable for INSERT, UPDATE and DELETE queries)
     * @param string $query The query to execute, variables to insert need to be replaced with '?'
     * @param array $parameters Variables that should replaces the '?'s in the query
     * @param bool $returnLastId TRUE, if the ID of the last inserted row should be returned instead of true/false
     * @return bool TRUE on success, FALSE on failure
     * @throws Exception In case of a database error
     */
    public static function executeQuery(string $query, array $parameters = array(), $returnLastId = false)
    {
        if (!isset(self::$connection)) {
            self::connect();
        }
        try {
            $statement = self::$connection->prepare($query);
            $result = $statement->execute($parameters);
        } catch (PDOException $e) {
            throw new Exception('Database query ['.$query.'] wasn\'t executed successfully.', $e->getCode(), $e);
        }
		
		if ($returnLastId) {
			return self::$connection->lastInsertId();
		}
        return $result;
    }
    
    /**
     * Execute a query that returns some kind of data (suitable for SELECT queries)
     * @param string $query The query to execute, variables to insert need to be replaced with '?'
     * @param array $parameters Variables that should replaces the '?'s in the query
     * @param bool $all TRUE, if multiple lines should be returned, FALSE, if only the first line is needed
     * @return array|false|mixed An associative array containing the query result in case of success, false if no rows
     *     were returned
     * @throws Exception In case of a database error
     */
    public static function fetchQuery(string $query, array $parameters = array(), bool $all = false)
    {
        if (!isset(self::$connection)) {
            self::connect();
        }
        try {
            $statement = self::$connection->prepare($query);
            $statement->execute($parameters);
        } catch (PDOException $e) {
            throw new Exception('Database query ['.$query.'] wasn\'t executed successfully.', /*$e->getCode()*/0, $e);
        }
        
        if ($statement->rowCount() === 0) {
            return false;
        }
        if ($all) {
            return $statement->fetchAll();
        } else {
            return $statement->fetch();
        }
    }
    
}