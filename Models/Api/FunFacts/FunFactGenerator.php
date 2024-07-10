<?php

namespace VoicesOfWynn\Models\Api\FunFacts;

class FunFactGenerator
{

    private const FUN_FACTS_LIBRARY_DIRECTORY = 'Models/Api/FunFacts/Library';

    /**
     * Loads and returns a random fun fact from the list of all fun facts
     * @return string A randomly picked fun fact
     */
    public function getRandomFact(): string
    {
        $files = glob(self::FUN_FACTS_LIBRARY_DIRECTORY . '/*.txt');
        $selectedFile = $files[array_rand($files)];
        return file_get_contents($selectedFile);
    }
}
