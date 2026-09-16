<?php

namespace VoicesOfWynn\Controllers\Api\LineReporting;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\LineReporting\ReportAdder;
use OpenApi\Attributes as OA;

/**
 * Kept alive only for mod clients that are already in players' hands and still post here. Staff
 * and bot tooling has moved to the .NET API (POST /vow-api/bot/reports/...); this route goes away
 * once a mod release pointing at POST /vow-api/reports has replaced the current one.
 */
#[OA\Tag(name: "Line Reporting", description: "Endpoints for reporting unvoiced lines.")]
class LineReporter extends ApiController
{

    public function process(array $args): int
    {
        switch ($args[0]) {
            case 'newUnvoicedLineReport':
                return $this->newReport();
            default:
                return 400;
        }
    }

    #[OA\Post(
        path: "/api/unvoiced-line-report/new",
        summary: "Create a new unvoiced line report (legacy mod clients only)",
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

        //A missing field used to be an undefined-key warning followed by a TypeError on the typed
        //parameters, which surfaced as a 500 rather than as the 406 the validation would have given
        if (!isset($_POST['full'], $_POST['npc'], $_POST['player'], $_POST['x'], $_POST['y'], $_POST['z'])) {
            return 406;
        }

        $reportAdder = new ReportAdder();
        return $reportAdder->createReport($_POST['full'], $_POST['npc'], $_POST['player'], $_POST['x'], $_POST['y'], $_POST['z']);
    }
}
