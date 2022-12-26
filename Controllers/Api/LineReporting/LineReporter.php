<?php

namespace VoicesOfWynn\Controllers\Api\LineReporting;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\LineReporting\ReportAdder;
use VoicesOfWynn\Models\Api\LineReporting\ReportManager;
use VoicesOfWynn\Models\Api\LineReporting\ReportReader;

class LineReporter extends ApiController
{

    public function process(array $args): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if (!isset($_REQUEST['apiKey']) && !isset($_PUT['apiKey'])) {
            return 401;
        }

        switch ($args[0]) {
            case 'newUnvoicedLineReport':
                return $this->newReport();
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

    private function newReport(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::REPORTING_API_KEY) {
            return 401;
        }
        $reportAdder = new ReportAdder();
        return $reportAdder->createReport($_POST['full'], $_POST['npc'], $_POST['player'], $_POST['x'], $_POST['y'], $_POST['z']);
    }

    private function getUnvoicedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
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

    private function getRawReportInfo(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
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

    private function updateReportStatus(): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
            return 405;
        }
        if ($_PUT['apiKey'] !== self::UPDATING_API_KEY) {
            return 401;
        }
        $reportManager = new ReportManager();
        $line = null;
        if (isset($_PUT['line'])) {
            $line = $_PUT['line'];
        }
        return $reportManager->updateReport($line, $_PUT['answer']);
    }

    private function resetForwardedLines(): int
    {
        parse_str(file_get_contents("php://input"),$_PUT);

        if ($_SERVER['REQUEST_METHOD'] !== 'PUT') {
            return 405;
        }
        if ($_PUT['apiKey'] !== self::UPDATING_API_KEY) {
            return 401;
        }
        $reportManager = new ReportManager();
        $npcName = null;
        if (isset($_PUT['npc'])) {
            $npcName = $_PUT['npc'];
        }
        return $reportManager->resetForwardedReports($npcName);
    }

    private function getAcceptedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('accepted'));
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }

    private function getNonRejectedLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('accepted', 'forwarded', 'unprocessed'));
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }

    private function getValidNpcLines(): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }
        if ($_REQUEST['apiKey'] !== self::COLLECTING_API_KEY) {
            return 401;
        }
        $reportReader = new ReportReader();
        $npcName = null;
        if (isset($_GET['npc'])) {
            $npcName = $_GET['npc'];
        }
        $responseCode = $reportReader->getReportsByNpc($npcName, array('fixed', 'accepted', 'forwarded', 'unprocessed'));
        if ($responseCode >= 400) {
            //An error occurred
            return $responseCode;
        }
        $reports = $reportReader->result;
        echo json_encode($reports, JSON_PRETTY_PRINT);
        return 200;
    }
}
