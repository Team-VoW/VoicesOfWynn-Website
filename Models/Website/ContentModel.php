<?php

namespace VoicesOfWynn\Models\Website;

abstract class ContentModel
{
    /**
     * Generates a degenerated (URL-safe, ASCII) version of a name
     * @param string $name The original name
     * @return string The degenerated name (lowercase, no spaces or special chars)
     */
    public static function degenerateName(string $name): string
    {
        $degenerated = strtolower($name);
        $degenerated = preg_replace('/[^a-z0-9]/', '', $degenerated);
        if ($degenerated === '') {
            throw new \InvalidArgumentException('Name must contain at least one alphanumeric character.');
        }
        return $degenerated;
    }
}
