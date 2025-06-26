<?php

namespace VoicesOfWynn\Models\Api\MessageBroadcast;

class BroadcastLoader
{
    private const CONFIG_FILE = 'BroadcastConfig/broadcast.ini';

    /**
     * Loads and returns all messages that should currently be broadcast, as an array of strings
     * (or an empty array if nothing should be broadcast).
     * @return array Array of string messages to broadcast
     */
    public function loadBroadcast(): array
    {
        $broadcasts = parse_ini_file(self::CONFIG_FILE, true);
        $result = [];
        foreach ($broadcasts as $broadcast) {
            if (strtotime($broadcast['since']) <= time() && strtotime($broadcast['until']) >= time()) {
                $result[] = $broadcast['content'];
            }
        }
        return $result;
    }
}