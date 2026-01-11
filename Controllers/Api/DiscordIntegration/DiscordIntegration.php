<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiErrorCode;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\DiscordIntegration\DiscordManager;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\UserException;
use OpenApi\Attributes as OA;

#[OA\Tag(name: "Discord Integration", description: "Endpoints for integrating with the Voices of Wynn Discord server.")]
class DiscordIntegration extends ApiController
{
    public function process(array $args): int
    {
        switch ($_SERVER['REQUEST_METHOD']) {
            case 'GET':
                return $this->get();
            case 'POST':
                return $this->post();
            default:
                return 405;
        }
    }

    #[OA\Get(
        path: "/api/discord-integration",
        summary: "Get Discord integration data",
        tags: ["Discord Integration"],
        parameters: [
            new OA\Parameter(name: "apiKey", in: "query", required: true, schema: new OA\Schema(type: "string", default: "testing")),
            new OA\Parameter(name: "action", in: "query", required: true, schema: new OA\Schema(type: "string", enum: ["getAllUsers"]))
        ],
        responses: [
            new OA\Response(response: 200, description: "Success", content: new OA\JsonContent(type: "array", items: new OA\Items(ref: "#/components/schemas/User"))),
            new OA\Response(response: 400, description: "Bad request - invalid action"),
            new OA\Response(response: 401, description: "Unauthorized - invalid API key"),
            new OA\Response(response: 500, description: "Internal server error", content: new OA\JsonContent(properties: [new OA\Property(property: "error", type: "string", description: "Error message")]))
        ]
    )]
    /**
     * @throws UserException
     */
    private function get(): int
    {
        if (!$this->checkApiKey(ApiKey::DISCORD_INTEGRATION, $_GET['apiKey'])) {
            return 401;
        }

        // Validate action parameter
        if (!isset($_GET['action']) || empty($_GET['action'])) {
            return $this->sendBadRequestError(ApiErrorCode::MISSING_ACTION_PARAMETER, 'The \'action\' parameter is required');
        }

        try {
            $manager = new DiscordManager();
            switch ($_GET['action']) {
                case 'getAllUsers':
                    $users = $manager->getAllUsers();
                    $decoded = json_decode($users, true);

                    // Check if the response contains an error
                    if (isset($decoded['error'])) {
                        echo $users;
                        return 500;
                    }

                    echo $users;
                    return 200;
                default:
                    return $this->sendBadRequestError(ApiErrorCode::UNKNOWN_ACTION, 'The requested action is not recognized');
            }
        } catch (\Exception $e) {
            error_log('DiscordIntegration::get error: ' . $e->getMessage());
            return 500;
        }
    }

    #[OA\Post(
        path: "/api/discord-integration",
        summary: "Synchronize a user",
        tags: ["Discord Integration"],
        requestBody: new OA\RequestBody(
            required: true,
            content: new OA\MediaType(
                mediaType: "application/x-www-form-urlencoded",
                schema: new OA\Schema(
                    required: ["apiKey", "action", "discordId", "discordName"],
                    properties: [
                        new OA\Property(property: "apiKey", type: "string", default: "testing"),
                        new OA\Property(property: "action", type: "string", enum: ["syncUser"]),
                        new OA\Property(property: "discordId", type: "integer"),
                        new OA\Property(property: "discordName", type: "string"),
                        new OA\Property(property: "imgurl", type: "string"),
                        new OA\Property(property: "name", type: "string"),
                        new OA\Property(property: "roles", type: "string")
                    ]
                )
            )
        ),
        responses: [
            new OA\Response(response: 200, description: "User updated"),
            new OA\Response(response: 201, description: "User created"),
            new OA\Response(response: 401, description: "Unauthorized")
        ]
    )]
    /**
     * @throws UserException
     */
    private function post(): int
    {
        if (!$this->checkApiKey(ApiKey::DISCORD_INTEGRATION, $_POST['apiKey'])) {
            return 401;
        }

        // Validate action parameter
        if (!isset($_POST['action']) || empty($_POST['action'])) {
            return $this->sendBadRequestError(ApiErrorCode::MISSING_ACTION_PARAMETER, 'The \'action\' parameter is required');
        }

        try {
            $manager = new DiscordManager();
            switch ($_POST['action']) {
                case 'syncUser':
                    // Validate required parameters
                    if (!isset($_POST['discordId']) || empty($_POST['discordId'])) {
                        return $this->sendBadRequestError(ApiErrorCode::MISSING_REQUIRED_PARAMETER, 'The \'discordId\' parameter is required');
                    }
                    if (!isset($_POST['discordName']) || empty($_POST['discordName'])) {
                        return $this->sendBadRequestError(ApiErrorCode::MISSING_REQUIRED_PARAMETER, 'The \'discordName\' parameter is required');
                    }

                    // Validate discordId is numeric
                    if (!is_numeric($_POST['discordId'])) {
                        return $this->sendBadRequestError(ApiErrorCode::INVALID_DISCORD_ID, 'The \'discordId\' must be a numeric value');
                    }

                    $imgurl = (isset($_POST['imgurl'])) ? $_POST['imgurl'] : null;
                    $name = (isset($_POST['name'])) ? $_POST['name'] : null;
                    $rolesJson = (isset($_POST['roles'])) ? $_POST['roles'] : null;

                    //Parse the JSON array of role names into array of DiscordRole objects
                    if (!is_null($rolesJson)) {
                        $roles = array();
                        $jsonData = json_decode($rolesJson);

                        // Check if JSON decoding failed
                        if (json_last_error() !== JSON_ERROR_NONE) {
                            return $this->sendBadRequestError(ApiErrorCode::INVALID_ROLES_JSON, 'The \'roles\' parameter must be valid JSON');
                        }

                        if (is_array($jsonData)) {
                            foreach ($jsonData as $roleName) {
                                $roles[] = new DiscordRole($roleName);
                            }
                        } else {
                            return $this->sendBadRequestError(ApiErrorCode::INVALID_ROLES_JSON, 'The \'roles\' parameter must be a JSON array');
                        }
                    } else {
                        $roles = null;
                    }

                    $responseCode = $manager->syncUser(
                        $_POST['discordId'],
                        $_POST['discordName'],
                        $imgurl,
                        $roles,
                        $name
                    );

                    if ($responseCode === 201) {
                        echo json_encode(['tempPassword' => $manager->lastUserPassword]);
                    }
                    return $responseCode;
                default:
                    return $this->sendBadRequestError(ApiErrorCode::UNKNOWN_ACTION, 'The requested action is not recognized');
            }
        } catch (UserException $e) {
            error_log('DiscordIntegration::post UserException: ' . $e->getMessage());
            return 500;
        } catch (\Exception $e) {
            error_log('DiscordIntegration::post error: ' . $e->getMessage());
            return 500;
        }
    }
}
