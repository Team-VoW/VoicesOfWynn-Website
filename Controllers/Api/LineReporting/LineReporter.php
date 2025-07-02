<?php

namespace VoicesOfWynn\Controllers\Api\LineReporting;

use DateTime;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\LineReporting\ReportAdder;
use VoicesOfWynn\Models\Api\LineReporting\ReportManager;
use VoicesOfWynn\Models\Api\LineReporting\ReportReader;
use OpenApi\Annotations as OA;

/**
 * @OA\Tag(
 *     name="Line Reporting",
 *     description="Endpoints for reporting and handling unvoiced lines."
 * )
 */
class LineReporter extends ApiController
{

    public function process(array $args): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if (!isset($_REQUEST['apiKey']) && !isset($_PUT['apiKey']) && $args[0] !== 'newUnvoicedLineReport') {
            return 401;
        }

        switch ($args[0]) {
            case 'newUnvoicedLineReport':
                return $this->newReport();
            case 'importLines':
                return $this->importLines();
            case 'listUnvoicedLineReport':
                return $this->getUnvoicedLines();
            case 'getRaw':
                return $this->getRawReportInfo();
            case 'updateReportStatus':
                return $this->updateReportStatus();
            case 'resetForwarded':
                return $this->resetForwardedLines();
            case 'getAcceptedReports':
                return $this->getAcceptedLines();
            case 'getActiveReports':
                return $this->getNonRejectedLines();
            case 'getValidReports':
                return $this->getValidNpcLines();
            default:
                return 400;
        }
    }

    /**
     * @OA\Post(
     *     path="/api/unvoiced-line-report/new",
     *     summary="Create a new unvoiced line report",
     *     tags={"Line Reporting"},
     *     @OA\RequestBody(
     *         required=true,
     *         @OA\MediaType(
     *             mediaType="application/x-www-form-urlencoded",
     *             @OA\Schema(
     *                 @OA\Property(
     *                     property="full",
     *                     type="string",
     *                     default="[1/1] Test: This is an example line."
     *                 ),
     *                 @OA\Property(
     *                     property="npc",
     *                     type="string",
     *                     default="test"
     *                 ),
     *                 @OA\Property(
     *                     property="player",
     *                     type="string",
     *                     default="anonymous"
     *                 ),
     *                 @OA\Property(
     *                     property="x",
     *                     type="integer"
     *                 ),
     *                 @OA\Property(
     *                     property="y",
     *                     type="integer"
     *                 ),
     *                 @OA\Property(
     *                     property="z",
     *                     type="integer"
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=201,
     *         description="Report created"
     *     ),
     *     @OA\Response(
     *         response=204,
     *         description="Report updated"
     *     ),
     *     @OA\Response(
     *         response=406,
     *         description="Not acceptable"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function newReport(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        $reportAdder = new ReportAdder();
        return $reportAdder->createReport($_POST['full'], $_POST['npc'], $_POST['player'], $_POST['x'], $_POST['y'], $_POST['z']);
    }

    /**
     * @OA\Post(
     *     path="/api/unvoiced-line-report/import",
     *     summary="Import unvoiced lines",
     *     description="Import multiple unvoiced lines with a specified status. Status codes: 'd' = draft, 'm' = missing, 'y' = yes/accepted, 'n' = no/rejected, 'v' = voiced/completed",
     *     tags={"Line Reporting"},
     *     @OA\RequestBody(
     *         required=true,
     *         @OA\MediaType(
     *             mediaType="application/x-www-form-urlencoded",
     *             @OA\Schema(
     *                 @OA\Property(
     *                     property="apiKey",
     *                     type="string",
     *                     default="testing"  
     *                 ),
     *                 @OA\Property(
     *                     property="lines[]",
     *                     type="array",
     *                     @OA\Items(type="string"),
     *                     example={"[1/2] Guard: Halt! Who goes there?"}
     *                 ),
     *                 @OA\Property(
     *                     property="status",
     *                     type="string",
     *                     enum={"d", "m", "y", "n", "v"},
     *                     description="Status to assign to imported lines: 'd' = draft, 'm' = missing, 'y' = yes/accepted, 'n' = no/rejected, 'v' = voiced/completed"
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=204,
     *         description="Success"
     *     ),
     *     @OA\Response(
     *         response=400,
     *         description="Bad request"
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function importLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        
        if (empty($_POST['status'])) {
            return 400;
        }
        $reportAdder = new ReportAdder();
        return $reportAdder->importLines($_POST['lines'], $_POST['status']);
    }

    /**
     * @OA\Get(
     *     path="/api/unvoiced-line-report/index",
     *     summary="Get all lines in draft state, after that set their state to forwarded",
     *     tags={"Line Reporting"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="testing")
     *     ),
     *     @OA\Parameter(
     *         name="npc",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\JsonContent(
     *             type="array",
     *             @OA\Items(type="object")
     *         )
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function getUnvoicedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_COLLECT, $_REQUEST['apiKey'])) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        $responseCode = $reportReader->listUnvoicedLineReports($npcName);
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }

    /**
     * @OA\Get(
     *     path="/api/unvoiced-line-report/raw",
     *     summary="Get raw report info",
     *     tags={"Line Reporting"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="testing")
     *     ),
     *     @OA\Parameter(
     *         name="line",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="[1/1] Test: This is an example line.")
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\JsonContent(type="object")
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=404,
     *         description="Not found"
     *     ),
     *     @OA\Response(
     *         response=406,
     *         description="Not acceptable"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function getRawReportInfo(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_COLLECT, $_REQUEST['apiKey'])) {
            return 401;
        }
        $reportReader = new ReportReader();
        $line = null;
        if (isset($_GET['line'])) {
            $line = $_GET['line'];
        }
        $responseCode = $reportReader->getRawReportData($line);
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reportInfo = $reportReader->result;
        echo json_encode($reportInfo, JSON_PRETTY_PRINT);
        return 200;
    }

    /**
     * @OA\Put(
     *     path="/api/unvoiced-line-report/resolve",
     *     summary="Update report status",
     *     tags={"Line Reporting"},
     *     @OA\RequestBody(
     *         required=true,
     *         @OA\MediaType(
     *             mediaType="application/x-www-form-urlencoded",
     *             @OA\Schema(
     *                 @OA\Property(
     *                     property="apiKey",
     *                     type="string",
     *                     default="testing"
     *                 ),
     *                 @OA\Property(
     *                     property="lines[]",
     *                     type="array",
     *                     @OA\Items(type="string")
     *                 ),
     *                 @OA\Property(
     *                     property="status",
     *                     type="string",
     *                     enum={"r", "d", "m", "y", "n", "v"}
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=204,
     *         description="Success"
     *     ),
     *     @OA\Response(
     *         response=400,
     *         description="Bad request"
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=404,
     *         description="Not found"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function updateReportStatus(): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_MODIFY, $_PUT['apiKey'])) {
            return 401;
        }
        $reportManager = new ReportManager();
        $lines = null;
        if (isset($_PUT['lines'])) {
            $lines = $_PUT['lines'];
        }
        if (is_null($lines) || count($lines) === 0) {
            return 400; //No lines provided
        }
        return $reportManager->updateReport($lines, $_PUT['status']);
    }

    /**
     * @OA\Put(
     *     path="/api/unvoiced-line-report/reset",
     *     summary="Reset forwarded lines",
     *     tags={"Line Reporting"},
     *     @OA\RequestBody(
     *         required=true,
     *         @OA\MediaType(
     *             mediaType="application/x-www-form-urlencoded",
     *             @OA\Schema(
     *                 @OA\Property(
     *                     property="apiKey",
     *                     type="string",
     *                     default="testing"
     *                 ),
     *                 @OA\Property(
     *                     property="npc",
     *                     type="string", default="",
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=204,
     *         description="Success"
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function resetForwardedLines(): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_MODIFY, $_PUT['apiKey'])) {
            return 401;
        }
        $reportManager = new ReportManager();
        $npcName = null;
        if (isset($_PUT['npc'])) {
            $npcName = $_PUT['npc'];
        }
        return $reportManager->resetForwardedReports($npcName);
    }

    /**
     * @OA\Get(
     *     path="/api/unvoiced-line-report/accepted",
     *     summary="Get accepted lines",
     *     tags={"Line Reporting"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="testing")
     *     ),
     *     @OA\Parameter(
     *         name="npc",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Parameter(
     *         name="minreports",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="integer")
     *     ),
     *     @OA\Parameter(
     *         name="youngerthan",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\JsonContent(
     *             type="array",
     *             @OA\Items(type="object")
     *         )
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function getAcceptedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_COLLECT, $_REQUEST['apiKey'])) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        $minReports = 1;
        $youngerThan = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        if (isset($_GET['minreports'])) {
            $minReports = $_GET['minreports'];
        }
        if (isset($_GET['youngerthan'])) {
            $youngerThan = DateTime::createFromFormat("Y-m-d", ($_GET['youngerthan']));
            $youngerThan->setTime(0, 0, 0);
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('accepted'), $minReports, $youngerThan);
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }

    /**
     * @OA\Get(
     *     path="/api/unvoiced-line-report/active",
     *     summary="Get non-rejected lines",
     *     tags={"Line Reporting"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="testing")
     *     ),
     *     @OA\Parameter(
     *         name="npc",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Parameter(
     *         name="minreports",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="integer")
     *     ),
     *     @OA\Parameter(
     *         name="youngerthan",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\JsonContent(
     *             type="array",
     *             @OA\Items(type="object")
     *         )
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function getNonRejectedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_COLLECT, $_REQUEST['apiKey'])) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        $minReports = 1;
        $youngerThan = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        if (isset($_GET['minreports'])) {
            $minReports = $_GET['minreports'];
        }
        if (isset($_GET['youngerthan'])) {
            $youngerThan = DateTime::createFromFormat("Y-m-d", ($_GET['youngerThan']));
            $youngerThan->setTime(0, 0, 0);
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('accepted', 'forwarded', 'unprocessed'), $minReports, $youngerThan);
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }

    /**
     * @OA\Get(
     *     path="/api/unvoiced-line-report/valid",
     *     summary="Get valid NPC lines",
     *     tags={"Line Reporting"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(type="string", default="testing")
     *     ),
     *     @OA\Parameter(
     *         name="npc",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Parameter(
     *         name="minreports",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="integer")
     *     ),
     *     @OA\Parameter(
     *         name="youngerthan",
     *         in="query",
     *         required=false,
     *         @OA\Schema(type="string")
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\JsonContent(
     *             type="array",
     *             @OA\Items(type="object")
     *         )
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error"
     *     )
     * )
     */
    private function getValidNpcLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_COLLECT, $_REQUEST['apiKey'])) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        $minReports = 1;
        $youngerThan = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        if (isset($_GET['minreports'])) {
            $minReports = $_GET['minreports'];
        }
        if (isset($_GET['youngerthan'])) {
            $youngerThan = DateTime::createFromFormat("Y-m-d", ($_GET['youngerthan']));
            $youngerThan->setTime(0, 0, 0);
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('fixed', 'accepted', 'forwarded', 'unprocessed'), $minReports, $youngerThan);
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }
}
