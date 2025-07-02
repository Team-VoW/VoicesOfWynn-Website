<?php

namespace VoicesOfWynn\Controllers\Api\DiscordIntegration;

use VoicesOfWynn\Controllers\Api\ApiController;
use VoicesOfWynn\Controllers\Api\ApiKey;
use VoicesOfWynn\Models\Api\DiscordIntegration\DiscordManager;
use VoicesOfWynn\Models\Website\DiscordRole;
use VoicesOfWynn\Models\Website\UserException;
use OpenApi\Annotations as OA;

/**
 * @OA\Tag(
 *     name="Discord Integration",
 *     description="Endpoints for integrating with the Voices of Wynn Discord server."
 * )
 */
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

    /**
     * @OA\Get(
     *     path="/api/discord-integration",
     *     summary="Get Discord integration data",
     *     tags={"Discord Integration"},
     *     @OA\Parameter(
     *         name="apiKey",
     *         in="query",
     *         required=true,
     *         @OA\Schema(
     *             type="string",
     *             default="testing"
     *         )
     *     ),
     *     @OA\Parameter(
     *         name="action",
     *         in="query",
     *         required=true,
     *         @OA\Schema(
     *             type="string",
     *             enum={"getAllUsers"}
     *         )
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="Success",
     *         @OA\MediaType(
     *             mediaType="application/json",
     *             @OA\Schema(
     *                 type="array",
     *                 @OA\Items(
     *                     type="object",
     *                     description="User object with all account information including roles"
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=400,
     *         description="Bad request - invalid action"
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized - invalid API key"
     *     ),
     *     @OA\Response(
     *         response=500,
     *         description="Internal server error",
     *         @OA\MediaType(
     *             mediaType="application/json",
     *             @OA\Schema(
     *                 type="object",
     *                 @OA\Property(
     *                     property="error",
     *                     type="string",
     *                     description="Error message"
     *                 )
     *             )
     *         )
     *     )
     * )
     * @throws UserException
     */
    private function get(): int
    {
        if (!$this->checkApiKey(ApiKey::DISCORD_INTEGRATION, $_GET['apiKey'])) {
            return 401;
        }

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
                return 400;
        }
    }

    /**
     * @OA\Post(
     *     path="/api/discord-integration",
     *     summary="Synchronize a user",
     *     tags={"Discord Integration"},
     *     @OA\RequestBody(
     *         required=true,
     *         @OA\MediaType(
     *             mediaType="application/x-www-form-urlencoded",
     *             @OA\Schema(
     *                 required={"apiKey", "action", "discordId", "discordName"},
     *                 @OA\Property(
     *                     property="apiKey",
     *                     type="string",
     *                     default="testing"
     *                 ),
     *                 @OA\Property(
     *                     property="action",
     *                     type="string",
     *                     enum={"syncUser"}
     *                 ),
     *                 @OA\Property(
     *                     property="discordId",
     *                     type="integer"
     *                 ),
     *                 @OA\Property(
     *                     property="discordName",
     *                     type="string"
     *                 ),
     *                 @OA\Property(
     *                     property="imgurl",
     *                     type="string"
     *                 ),
     *                 @OA\Property(
     *                     property="name",
     *                     type="string"
     *                 ),
     *                 @OA\Property(
     *                     property="roles",
     *                     type="string"
     *                 )
     *             )
     *         )
     *     ),
     *     @OA\Response(
     *         response=200,
     *         description="User updated"
     *     ),
     *      @OA\Response(
     *         response=201,
     *         description="User created"
     *     ),
     *     @OA\Response(
     *         response=401,
     *         description="Unauthorized"
     *     )
     * )
     * @throws UserException
     */
    private function post(): int
    {
        if (!$this->checkApiKey(ApiKey::DISCORD_INTEGRATION, $_POST['apiKey'])) {
            return 401;
        }

        $manager = new DiscordManager();
        switch ($_POST['action']) {
            case 'syncUser':
                $imgurl = (isset($_POST['imgurl'])) ? $_POST['imgurl'] : null;
                $name = (isset($_POST['name'])) ? $_POST['name'] : null;
                $rolesJson = (isset($_POST['roles'])) ? $_POST['roles'] : null;

                //Parse the JSON array of role names into array of DiscordRole objects
                if (!is_null($rolesJson)) {
                    $roles = array();
                    $jsonData = json_decode($rolesJson);
                    foreach ($jsonData as $roleName) {
                        $roles[] = new DiscordRole($roleName);
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
                return 400;
        }
    }
}
