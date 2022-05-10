<?php


namespace VoicesOfWynn\Controllers;

use PDO;
use PDOException;

class Api extends Controller
{
	const REPORTING_API_KEY = '';
	const COLLECTING_API_KEY = '';
	const UPDATING_API_KEY = '';
	
	const ANONYMOUS_REPORT_NAME_INDICATOR = "Anonymous";
	
	/**
	 * @inheritDoc
	 */
	public function process(array $args): bool
	{
		parse_str(file_get_contents("php://input"),$_PUT);
		
		if (!isset($_REQUEST['apiKey']) && !isset($_PUT['apiKey'])) {
			header("HTTP/1.1 403 Forbidden");
			die();
		}
		
		header('Content-Type: application/json');
		switch ($args[0]) {
			case 'newUnvoicedLineReport':
				if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_REQUEST['apiKey'] !== self::REPORTING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->newUnvoicedLineReport(array());
				break;
			case 'listUnvoicedLineReport':
				if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->listUnvoicedLineReports(array());
				break;
			case 'getRaw':
				if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->getRawReportData(array(@$_GET['line']));
				break;
			case 'updateReportStatus':
				if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_PUT['apiKey'] !== self::UPDATING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->updateReport(array($_PUT['line'], $_PUT['answer'])); //answer must be either "y", "n", "v" or "r" (case senstive)
				break;
			case 'resetForwarded':
				if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_PUT['apiKey'] !== self::UPDATING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->resetForwardedStatus(array());
				break;
			case 'getAcceptedReports':
				if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
					header("HTTP/1.1 405 Method Not Allowed");
					die();
				}
				if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
					header("HTTP/1.1 401 Unauthorized");
					die();
				}
				$this->getAcceptedReports(array());
				break;
			default:
				header("HTTP/1.1 400 Bad Request");
				die();
		}
		
		die();
	}
	
	private function connectToDb(): PDO
	{
		try {
			return new PDO('mysql:host=localhost;dbname=unvoicedlines', 'root', '', array(
				PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
				PDO::MYSQL_ATTR_INIT_COMMAND => "SET NAMES utf8",
				PDO::ATTR_EMULATE_PREPARES => false
			));
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo json_encode("Connection with the database couldn't be established. If the problem persists, contact the webmaster, please.");
			die();
		}
	}
	
	/**
	 * Processing method for a POST request creating a new report
	 * @param array $args
	 * @return bool
	 */
	private function newUnvoicedLineReport(array $args): void
	{
		$this->checkLength($_POST['full'], 1, 511);
		$this->checkLength($_POST['npc'], 0, 127);
		$this->checkLength($_POST['player'], 1, 32); //Minecraft names's length can be 16 characters at maximum, but we need 64 for sha256 hashes of IPs
		$this->checkRange($_POST['x'], -8388608, 8388607);
		$this->checkRange($_POST['y'], -8388608, 8388607);
		$this->checkRange($_POST['z'], -8388608, 8388607);
		
		$author = $_POST['player']
		if ($author === self::ANONYMOUS_REPORT_NAME_INDICATOR) {
			$author = hash('sha256', $_SERVER['REMOTE_ADDR']);
		}
		
		$db = $this->connectToDb();
		
		try {
			//Check whether this line has already been reported
			$statement = $db->prepare('SELECT report_id FROM report WHERE chat_message = ? LIMIT 1');
			$statement->execute(array($_POST['full']));
			if ($statement->rowCount() === 0) {
				//Completely new report
				$statement = $db->prepare('INSERT INTO report (chat_message, npc_name, player, pos_x, pos_y, pos_z) VALUES (?,?,?,?,?,?)');
				$result = $statement->execute(array(
					$_POST['full'],
					$_POST['npc'],
					$author,
					$_POST['x'],
					$_POST['y'],
					$_POST['z'],
				));
			}
			else {
				//Updating existing report
				$existingReportId = $statement->fetch()['report_id'];
				$statement = $db->prepare('UPDATE report SET pos_x = (pos_x * reported_times + ?) / 2, pos_y = (pos_y * reported_times + ?) / 2, pos_z = (pos_z * reported_times + ?) / 2, reported_times = reported_times + 1 WHERE report_id = ?;');
				$result = $statement->execute(array(
					$_POST['x'],
					$_POST['y'],
					$_POST['z'],
					$existingReportId,
				));
			}
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo json_encode("The report couldn't be saved. If the problem persists, contact the webmaster, please.");
			die();
		}
		
		header("HTTP/1.1 201 Created");
		die();
	}
	
	/**
	 * Processing method for a GET request used to list all reports
	 * @param array $args
	 * @return bool
	 */
	private function listUnvoicedLineReports(array $args): bool
	{
		$query = null;
		if (isset($_GET['npc'])) {
			$npcName = $_GET['npc'];
			$query1 = 'SELECT npc_name AS "NPC",pos_x AS "X",pos_y AS "Y",pos_z AS "Z",player AS "reporter",chat_message AS "message" FROM report WHERE status = "unprocessed" AND npc_name = ?';
			$query2 = 'UPDATE report SET status = "forwarded" WHERE status = "unprocessed" AND npc_name = ?';
			$parameters = array($npcName);
		}
		else {
			$query1 = 'SELECT npc_name AS "NPC",pos_x AS "X",pos_y AS "Y",pos_z AS "Z",player AS "reporter",chat_message AS "message" FROM report WHERE status = "unprocessed"';
			$query2 = 'UPDATE report SET status = "forwarded" WHERE status = "unprocessed"';
			$parameters = array();
		}
		
		$db = $this->connectToDb();
		
		try {
			$statement = $db->prepare($query1);
			$statement->execute($parameters);
			$result = $statement->fetchAll(PDO::FETCH_ASSOC);
			$statement = $db->prepare($query2);
			$statement->execute($parameters);
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo json_encode("Reports couldn't be fetched. If the problem persists, contact the webmaster, please.");
			die();
		}
		
		header("HTTP/1.1 200 OK");
		echo json_encode($result, JSON_PRETTY_PRINT);
		die();
	}
	
	/**
	 * Processing method for a GET request used to get all information about a single report
	 * Partial search is performed and if more lines are returned, only the first one is outputted
	 * @param array $args Must contain the chat message (or its part) as the first element
	 * @return bool
	 */
	private function getRawReportData(array $args): bool
	{
		$line = (isset($args[0])) ? $args[0] : null;
		if (is_null($line)) {
			header("HTTP/1.1 406 Not Acceptable");
			die();
		}
		
		$db = $this->connectToDb();
		
		try {
			$statement = $db->prepare("SELECT * FROM report WHERE chat_message LIKE ? LIMIT 1");
			$statement->execute(array('%'.$line.'%'));
			$result = $statement->fetch(PDO::FETCH_ASSOC);
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo json_encode("Reports couldn't be fetched. If the problem persists, contact the webmaster, please.");
			die();
		}
		
		header("HTTP/1.1 200 OK");
		echo json_encode($result, JSON_PRETTY_PRINT);
		die();
	}
	
	/**
	 * Processing method for a PUT request used to update the status of a single report or a delete it (if the verdict variable is set to "r")
	 * @param array $args Must contain the chat message as the first element and the verdict as the second one
	 * @return bool
	 */
	private function updateReport(array $args): bool
	{
		$chatMessage = $args[0];
        	$db = $this->connectToDb();
		
		if ($verdict === 'r') { //Deleting the report
			try {
				$statement = $db->prepare('DELETE FROM report WHERE chat_message = ? LIMIT 1');
				$statement->execute(array($chatMessage));
			} catch (PDOException $e) {
    				header("HTTP/1.1 500 Internal Server Error");
    				echo json_encode("The report couldn't be deleted. If the problem persists, contact the webmaster, please.");
    				die();
    			}
    			header("HTTP/1.1 204 No content");
			die();
		}
		
		$verdict = ($args[1] === 'y') ? "accepted" : (($args[1] === 'n') ? "rejected" : (($args[1] === 'v') ? "fixed" : null));
		
		if (is_null($verdict)) {
			header("HTTP/1.1 406 Not Acceptable");
			die();
		}
		try {
			$statement = $db->prepare('UPDATE report SET status = ? WHERE chat_message = ? LIMIT 1');
			$statement->execute(array($verdict, $chatMessage));
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo json_encode("The report couldn't be updated. If the problem persists, contact the webmaster, please.");
			die();
		}
		
		header("HTTP/1.1 204 No content");
		die();
	}
	
	/**
	 * Processing method for a PUT request used to reset the status of all unresolved reports
	 * If a NPC name is sent with the request under name "npc", only reports of such NPC are reset
	 * @param array $args
	 * @return bool
	 */
	private function resetForwardedStatus(array $args): bool
	{
		$query = null;
		if (isset($_PUT['npc'])) {
			$npcName = $_GET['npc'];
			$query = 'UPDATE report SET status = "unprocessed" WHERE npc_name = ? AND status = "forwarded"';
			$parameters = array($npcName);
		}
		else {
			$query = 'UPDATE report SET status = "unprocessed" AND status = "forwarded"';
			$parameters = array();
		}
		
		$db = $this->connectToDb();
		
		try {
			$statement = $db->prepare($query);
			$statement->execute($parameters);
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo "The reports couldn't be reset. If the problem persists, contact the webmaster, please.";
			die();
		}
		
		header("HTTP/1.1 204 No Content");
		die();
	}
	
	/**
	 * Processing method for a GET request used to list all accepted records
	 * @param array $args
	 * @return bool
	 */
	private function getAcceptedReports(array $args): bool
	{
		$query = null;
		if (isset($_GET['npc'])) {
			$npcName = $_GET['npc'];
			$query = 'SELECT npc_name AS "NPC",pos_x AS "X",pos_y AS "Y",pos_z AS "Z",chat_message AS "message" FROM report WHERE status = "accepted" AND npc_name = ?';
			$parameters = array($npcName);
		}
		else {
			$query = 'SELECT npc_name AS "NPC",pos_x AS "X",pos_y AS "Y",pos_z AS "Z",chat_message AS "message" FROM report WHERE status = "accepted"';
			$parameters = array();
		}
		
		$db = $this->connectToDb();
		
		try {
			$statement = $db->prepare($query);
			$statement->execute($parameters);
			$result = $statement->fetchAll(PDO::FETCH_ASSOC);
		} catch (PDOException $e) {
			header("HTTP/1.1 500 Internal Server Error");
			echo "The report couldn't be fetched. If the problem persists, contact the webmaster, please.";
			die();
		}
		
		header("HTTP/1.1 200 OK");
		echo json_encode($result, JSON_PRETTY_PRINT);
		die();
	}
	
	/**
	 * Method checking a length of an input string
	 * If the length requirement is not fullfilled, the script execution is terminated and the 406 Not Acceptable HTTP status is sent back
	 * @param string $str String to check
	 * @param int $min Minimal length
	 * @param int $max Maximal length
	 */
	private function checkLength(string $str, int $min, int $max): void
	{
		if (strlen($str) < $min || strlen($str) > $max) {
			header("HTTP/1.1 406 Not Acceptable");
			die();
		}
	}
	
	/**
	 * Method checking a size of an input integer
	 * If the range requirement is not fulfilled, the script execution is terminated and the 406 Not Acceptable HTTP status is sent back
	 * @param int $num Integer to check
	 * @param int $min Minimal length
	 * @param int $max Maximal length
	 */
	private function checkRange(int $num, int $min, int $max): void
	{
		if ($num < $min || $num > $max) {
			header("HTTP/1.1 406 Not Acceptable");
			die();
		}
	}
}

