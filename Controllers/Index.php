<?php


namespace VoicesOfWynn\Controllers;


class Index extends Controller
{
    
    public function process(array $args): bool
    {
        self::$data['base_title'] = 'Voices of Wynn';
        self::$data['base_description'] = 'Welcome to the webpage of Voices of Wynn - a mod for MMORPG Minecraft server Wynncraft that adds voices to many in-game NPCs.';
        self::$data['base_keywords'] = 'Minecraft,Wynncraft,Mod,Voice';
        
        self::$views[] = 'index';
        return true;
    }
}