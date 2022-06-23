<?php


namespace VoicesOfWynn\Models;


use Exception;
use PDO;
use PDOException;

class Db
{
    private string $host;
    private string $database;
    private string $username;
    private string $password;
    private array $settings = array(
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::MYSQL_ATTR_INIT_COMMAND => "SET NAMES utf8",
        PDO::ATTR_EMULATE_PREPARES => false
    );
    
    private PDO $connection;

    public function __construct($credentialsFilePath) {
        $credentials = parse_ini_file($credentialsFilePath);
        $this->host = $credentials['host'];
        $this->database = $credentials['database'];
        $this->username = $credentials['username'];
        $this->password = $credentials['password'];
    }

    /**
     * Database wrapper method connecting to the database.
     */
    private function connect()
    {
        try {
            $this->connection = new PDO('mysql:host='.$this->host.';dbname='.$this->database, $this->username,
                $this->password, $this->settings);
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
     * @return bool|string TRUE on success, FALSE on failure; in case the last parameter is set to TRUE, the ID of the
     * inserter row is returned
     * @throws Exception In case of a database error
     */
    public function executeQuery(string $query, array $parameters = array(), bool $returnLastId = false)
    {
        if (!isset($this->connection)) {
            $this->connect();
        }
        try {
            $statement = $this->connection->prepare($query);
            $result = $statement->execute($parameters);
        } catch (PDOException $e) {
            throw new Exception('Database query ['.$query.'] wasn\'t executed successfully.', $e->getCode(), $e);
        }
		
		if ($returnLastId) {
			return $this->connection->lastInsertId();
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
    public function fetchQuery(string $query, array $parameters = array(), bool $all = false)
    {
        if (!isset($this->connection)) {
            self::connect();
        }
        try {
            $statement = $this->connection->prepare($query);
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