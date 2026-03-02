<?php

namespace VoicesOfWynn\Controllers\Api\Other;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Models\Api\LineReporting\ReportReader;
use VoicesOfWynn\Models\Website\ContentManager;
use OpenApi\Attributes as OA;

class NpcSearch extends ApiController
{
    #[OA\Get(
        path: "/api/npc/search",
        summary: "Search NPCs by name",
        tags: ["NPC"],
        parameters: [
            new OA\Parameter(name: "q", in: "query", required: true, schema: new OA\Schema(type: "string"), description: "Search term (substring match)")
        ],
        responses: [
            new OA\Response(
                response: 200,
                description: "List of matching NPCs (max 100)",
                content: new OA\JsonContent(
                    type: "array",
                    items: new OA\Items(
                        properties: [
                            new OA\Property(property: "npc_id", type: "integer"),
                            new OA\Property(property: "name", type: "string"),
                            new OA\Property(
                                property: "last_seen_at",
                                nullable: true,
                                properties: [
                                    new OA\Property(property: "x", type: "integer"),
                                    new OA\Property(property: "y", type: "integer"),
                                    new OA\Property(property: "z", type: "integer")
                                ],
                                type: "object"
                            )
                        ]
                    )
                )
            ),
            new OA\Response(response: 405, description: "Method not allowed")
        ]
    )]
    public function process(array $args): int
    {
        if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
            return 405;
        }

        $query = $_GET['q'] ?? '';
        if ($query === '') {
            echo json_encode([]);
            return 200;
        }

        $npcs = (new ContentManager())->searchNpcs($query);
        $positions = (new ReportReader())->getLastPositionsByNpcNames(array_column($npcs, 'degenerated_name'));

        $results = [];
        foreach ($npcs as $npc) {
            $pos = $positions[$npc['degenerated_name']] ?? null;
            $results[] = [
                'npc_id'       => $npc['npc_id'],
                'name'         => $npc['name'],
                'last_seen_at' => $pos,
            ];
        }

        echo json_encode($results);
        return 200;
    }
}
