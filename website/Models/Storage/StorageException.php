<?php

namespace VoicesOfWynn\Models\Storage;

class StorageException extends \Exception {
    private string $operation;
    private string $path;

    public function __construct(string $message, string $operation, string $path, ?\Throwable $previous = null) {
        parent::__construct($message, 0, $previous);
        $this->operation = $operation;
        $this->path = $path;
    }

    public function getOperation(): string { return $this->operation; }
    public function getPath(): string { return $this->path; }
}
