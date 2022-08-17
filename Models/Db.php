<?php


namespace VoicesOfWynn\Models;


use Exception;
use PDO;
use PDOException;

class Db
{
    /**
     * Associative array with all active PDO connections to prevent from creating many duplicate objects.
     * Keys are in the format of "[database name]_[user name]" (without "[]")
     * Values are the PDO objects themselves
     * @var array
     */
    private static array $connections = array();

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
    private function connect(): bool
    {
        if (isset(self::$connections[$this->database.'_'.$this->username])) {
            //Connection to this database with this account already exists, we'll just copy a reference to it
            $this->connection = self::$connections[$this->database.'_'.$this->username];
            return true;
        }

        try {
            $this->connection = new PDO('mysql:host='.$this->host.';dbname='.$this->database, $this->username, $this->password, $this->settings);
            self::$connections[$this->database.'_'.$this->username] = $this->connection; //Save the connection for later use
        } catch (PDOException $e) {
            return false;
        }
        return true;
    }

    /**
     * Method closing all existing PDO connections by destroying the references to their objects
     * @return void
     */
    public function closeAllConnections(): void
    {
        self::$connections = array();
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
        //try { //Uncomment these lines for debugging purposes, when you need to see the queries causing the errors
        $statement = $this->connection->prepare($query);
        $result = $statement->execute($parameters);
        //} catch (PDOException $e) {
        //    throw new Exception('Database query ['.$query.'] wasn\'t executed successfully.', $e->getCode(), $e);
        //}

		if ($returnLastId) {
			return $this->connection->lastInsertId();
		}
        return $result;
    }

    /**
     * Execute a query that returns some kind of data (suitable for SELECT queries)
     * @param string $query The query to execute, variables to insert need to be replaced with '?'
     * @param array $parameters Variables that should replace the '?'s in the query
     * @param bool $all TRUE, if multiple lines should be returned, FALSE, if only the first line is needed
     * @param int $fetchMethod Constant of the PDO object, that will be passed to the fetch(All)() method
     * @return array|false|mixed An associative array containing the query result in case of success, false if no rows
     *     were returned
     * @throws PDOException In case of a database error
     */
    public function fetchQuery(string $query, array $parameters = array(), bool $all = false, int $fetchMethod = PDO::FETCH_ASSOC)
    {
        if (!isset($this->connection)) {
            $this->connect();
        }

        //try { //Uncomment these lines for debugging purposes, when you need to see the queries causing the errors
        $statement = $this->connection->prepare($query);
        $statement->execute($parameters);
        //} catch (PDOException $e) {
        //    throw new Exception('Database query ['.$query.'] wasn\'t executed successfully.', /*$e->getCode()*/0, $e);
        //}

        if ($statement->rowCount() === 0) {
            return false;
        }
        if ($all) {
            return $statement->fetchAll($fetchMethod);
        } else {
            return $statement->fetch($fetchMethod);
        }
    }

    /**
     * Starts a transaction
     * @return bool
     */
    public function startTransaction(): bool
    {
        if (!isset($this->connection)) {
            $this->connect();
        }

        return $this->connection->beginTransaction();
    }

    /**
     * Rollbacks an active transaction
     * @return bool
     */
    public function rollbackTransaction(): bool
    {
        if (!isset($this->connection)) {
            $this->connect();
        }

        return $this->connection->rollBack();
    }

    /**
     * Commits an active transaction
     * @return bool
     */
    public function commitTransaction(): bool
    {
        if (!isset($this->connection)) {
            $this->connect();
        }

        return $this->connection->commit();
    }
}
