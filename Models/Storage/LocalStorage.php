<?php

namespace VoicesOfWynn\Models\Storage;

class LocalStorage implements StorageInterface {
    private string $basePath;

    public function __construct(string $basePath = 'dynamic/') {
        $this->basePath = rtrim($basePath, '/') . '/';
    }

    public function upload(string $sourcePath, string $destinationPath, ?string $contentType = null): bool {
        $fullPath = $this->basePath . $destinationPath;
        $dir = dirname($fullPath);
        if (!is_dir($dir)) {
            mkdir($dir, 0755, true);
        }
        return move_uploaded_file($sourcePath, $fullPath) || copy($sourcePath, $fullPath);
    }

    public function delete(string $path): bool {
        $fullPath = $this->basePath . $path;
        if (file_exists($fullPath)) {
            return unlink($fullPath);
        }
        return true; // File doesn't exist = success
    }

    public function deleteByPrefix(string $prefix): array {
        $pattern = $this->basePath . $prefix . '*';
        $files = glob($pattern);
        $deleted = [];
        foreach ($files as $file) {
            if (unlink($file)) {
                $deleted[] = str_replace($this->basePath, '', $file);
            }
        }
        return $deleted;
    }

    public function rename(string $oldPath, string $newPath): bool {
        return rename($this->basePath . $oldPath, $this->basePath . $newPath);
    }

    public function copy(string $sourcePath, string $destinationPath): bool {
        return copy($this->basePath . $sourcePath, $this->basePath . $destinationPath);
    }

    public function exists(string $path): bool {
        return file_exists($this->basePath . $path);
    }

    public function getUrl(string $path, bool $cacheBust = false): string {
        $url = $this->basePath . $path;
        if ($cacheBust) {
            $url .= '?' . rand(0, 31);
        }
        return $url;
    }

    public function getBaseUrl(): string {
        return $this->basePath;
    }
}
