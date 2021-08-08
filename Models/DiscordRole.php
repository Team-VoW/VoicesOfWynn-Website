<?php

namespace VoicesOfWynn\Models;

class DiscordRole
{
    public string $name;
    public string $color;
    public int $weight;
    
    public function __construct(string $name, string $color = "FFFFFF", int $weight = 0) {
        $this->name = $name;
        $this->color = $color;
        $this->weight = $weight;
    }
}

