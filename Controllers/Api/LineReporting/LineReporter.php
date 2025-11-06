<?php

namespace VoicesOfWynn\Controllers\Api\LineReporting;

use DateTime;
use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\LineReporting\ReportAdder;
use VoicesOfWynn\Models\Api\LineReporting\ReportManager;
use VoicesOfWynn\Models\Api\LineReporting\ReportReader;
use OpenApi\Attributes as OA;

#[OA\Tag(name: "Line Reporting", description: "Endpoints for reporting and handling unvoiced lines.")]
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
                return $this->sendBadRequestError('UNKNOWN_ACTION', 'The requested action is not recognized');
        }
    }

    #[OA\Post(
        path: "/api/unvoiced-line-report/new",
        summary: "Create a new unvoiced line report",
        tags: ["Line Reporting"],
        requestBody: new OA\RequestBody(
            required: true,
            content: new OA\MediaType(
                mediaType: "application/x-www-form-urlencoded",
                schema: new OA\Schema(
                    properties: [
                        new OA\Property(property: "full", type: "string", default: "[1/1] Test: This is an example line."),
                        new OA\Property(property: "npc", type: "string", default: "test"),
                        new OA\Property(property: "player", type: "string", default: "anonymous"),
                        new OA\Property(property: "x", type: "integer"),
                        new OA\Property(property: "y", type: "integer"),
                        new OA\Property(property: "z", type: "integer")
                    ],
                    required: ["full", "npc", "player"]
                )
            )
        ),
        responses: [
            new OA\Response(response: 201, description: "Report created"),
            new OA\Response(response: 204, description: "Report updated"),
            new OA\Response(response: 406, description: "Not acceptable"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
    private function newReport(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        $reportAdder = new ReportAdder();
        return $reportAdder->createReport($_POST['full'], $_POST['npc'], $_POST['player'], $_POST['x'], $_POST['y'], $_POST['z']);
    }

    #[OA\Post(
        path: "/api/unvoiced-line-report/import",
        summary: "Import unvoiced lines",
        description: "Import multiple unvoiced lines with a specified status. Status codes: 'd' = draft, 'm' = missing, 'y' = yes/accepted, 'n' = no/rejected, 'v' = voiced/completed",
        tags: ["Line Reporting"],
        requestBody: new OA\RequestBody(
            required: true,
            content: new OA\MediaType(
                mediaType: "application/x-www-form-urlencoded",
                schema: new OA\Schema(
                    properties: [
                        new OA\Property(property: "apiKey", type: "string", default: "testing"),
                        new OA\Property(property: "lines[]", type: "array", items: new OA\Items(type: "string"), example: ["[1/2] Guard: Halt! Who goes there?"]),
                        new OA\Property(property: "status", type: "string", enum: ["d", "m", "y", "n", "v"], 
                        description: "Status to assign to imported lines: 'd' = draft, 'm' = missing, 'y' = yes/accepted, 'n' = no/rejected, 'v' = voiced/completed")
                    ],
                    required: ["apiKey", "lines", "status"]
                )
            )
        ),
        responses: [
            new OA\Response(response: 204, description: "Success"),
            new OA\Response(response: 400, description: "Bad request"),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
    private function importLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        
        if (!$this->checkApiKey(ApiKey::LINE_REPORT_MODIFY, $_POST['apiKey'])) {
            return 401;
        }

        if (empty($_POST['status'])) {
            return $this->sendBadRequestError('MISSING_STATUS', 'The \'status\' parameter is required');
        }
        $reportAdder = new ReportAdder();
        return $reportAdder->importLines($_POST['lines'], $_POST['status']);
    }

    #[OA\Get(
        path: "/api/unvoiced-line-report/index",
        summary: "Get all lines in draft state, after that set their state to forwarded",
        tags: ["Line Reporting"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "npc", in: "query", required: false, schema: new OA\Schema(type: "string"))
        ],
        responses: [
            new OA\Response(
                response: 200, 
                description: "Success", 
                content: new OA\JsonContent(
                    type: "array", 
                    items: new OA\Items(ref: "#/components/schemas/UnvoicedLineReport")
                )
            ),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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

    #[OA\Get(
        path: "/api/unvoiced-line-report/raw",
        summary: "Get raw report info",
        tags: ["Line Reporting"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "line", in: "query", required: true, schema: new OA\Schema(type: "string", default: "[1/1] Test: This is an example line."))
        ],
        responses: [
            new OA\Response(
                response: 200, 
                description: "Success", 
                content: new OA\JsonContent(ref: "#/components/schemas/Report")
            ),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 404, description: "Not found"),
            new OA\Response(response: 406, description: "Not acceptable"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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

    #[OA\Put(
        path: "/api/unvoiced-line-report/resolve",
        summary: "Update report status",
        tags: ["Line Reporting"],
        requestBody: new OA\RequestBody(
            required: true,
            content: new OA\MediaType(
                mediaType: "application/x-www-form-urlencoded",
                schema: new OA\Schema(
                    properties: [
                        new OA\Property(property: "apiKey", type: "string", default: "testing"),
                        new OA\Property(property: "lines[]", type: "array", items: new OA\Items(type: "string")),
                        new OA\Property(property: "status", type: "string", enum: ["r", "d", "m", "y", "n", "v"])
                    ],
                    required: ["apiKey", "lines", "status"]
                )
            )
        ),
        responses: [
            new OA\Response(response: 204, description: "Success"),
            new OA\Response(response: 400, description: "Bad request"),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 404, description: "Not found"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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
            return $this->sendBadRequestError('NO_LINES_PROVIDED', 'No lines were provided or the lines array is empty');
        }
        return $reportManager->updateReport($lines, $_PUT['status']);
    }

    #[OA\Put(
        path: "/api/unvoiced-line-report/reset",
        summary: "Reset forwarded lines",
        tags: ["Line Reporting"],
        requestBody: new OA\RequestBody(
            required: true,
            content: new OA\MediaType(
                mediaType: "application/x-www-form-urlencoded",
                schema: new OA\Schema(
                    properties: [
                        new OA\Property(property: "apiKey", type: "string", default: "testing"),
                        new OA\Property(property: "npc", type: "string", default: "")
                    ],
                    required: ["apiKey"]
                )
            )
        ),
        responses: [
            new OA\Response(response: 204, description: "Success"),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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

    #[OA\Get(
        path: "/api/unvoiced-line-report/accepted",
        summary: "Get accepted lines",
        tags: ["Line Reporting"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "npc", in: "query", required: false, schema: new OA\Schema(type: "string")),
            new OA\Parameter(name: "minreports", in: "query", required: false, schema: new OA\Schema(type: "integer")),
            new OA\Parameter(name: "youngerthan", in: "query", required: false, schema: new OA\Schema(type: "string"))
        ],
        responses: [
            new OA\Response(
                response: 200, 
                description: "Success", 
                content: new OA\JsonContent(
                    type: "array", 
                    items: new OA\Items(ref: "#/components/schemas/UnvoicedLineReport")
                )
            ),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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

    #[OA\Get(
        path: "/api/unvoiced-line-report/active",
        summary: "Get non-rejected lines",
        tags: ["Line Reporting"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "npc", in: "query", required: false, schema: new OA\Schema(type: "string")),
            new OA\Parameter(name: "minreports", in: "query", required: false, schema: new OA\Schema(type: "integer")),
            new OA\Parameter(name: "youngerthan", in: "query", required: false, schema: new OA\Schema(type: "string"))
        ],
        responses: [
            new OA\Response(
                response: 200, 
                description: "Success", 
                content: new OA\JsonContent(
                    type: "array", 
                    items: new OA\Items(ref: "#/components/schemas/UnvoicedLineReport")
                )
            ),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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

    #[OA\Get(
        path: "/api/unvoiced-line-report/valid",
        summary: "Get valid NPC lines",
        tags: ["Line Reporting"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "npc", in: "query", required: false, schema: new OA\Schema(type: "string")),
            new OA\Parameter(name: "minreports", in: "query", required: false, schema: new OA\Schema(type: "integer")),
            new OA\Parameter(name: "youngerthan", in: "query", required: false, schema: new OA\Schema(type: "string"))
        ],
        responses: [
            new OA\Response(
                response: 200, 
                description: "Success", 
                content: new OA\JsonContent(
                    type: "array", 
                    items: new OA\Items(ref: "#/components/schemas/UnvoicedLineReport")
                )
            ),
            new OA\Response(response: 401, description: "Unauthorized"),
            new OA\Response(response: 500, description: "Internal server error")
        ]
    )]
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
