<?php

namespace VoicesOfWynn\Models\Storage;

interface StorageInterface {
    /**
     * Upload a file to storage
     * @param string $sourcePath Local file path to upload
     * @param string $destinationPath Target path in storage (e.g., 'avatars/1.jpg')
     * @param string|null $contentType MIME type (auto-detected if null)
     * @return bool Success
     * @throws StorageException On failure
     */
    public function upload(string $sourcePath, string $destinationPath, ?string $contentType = null): bool;

    /**
     * Delete a file from storage
     * @param string $path Path to delete
     * @return bool Success (true even if file didn't exist)
     * @throws StorageException On failure
     */
    public function delete(string $path): bool;

    /**
     * Delete files matching a prefix (NOT glob - just prefix match)
     * @param string $prefix Path prefix (e.g., 'avatars/123.' matches 'avatars/123.jpg', 'avatars/123.png')
     * @return array List of deleted paths
     * @throws StorageException On failure
     */
    public function deleteByPrefix(string $prefix): array;

    /**
     * Rename/move a file
     * @param string $oldPath Current path
     * @param string $newPath New path
     * @return bool Success
     * @throws StorageException On failure
     */
    public function rename(string $oldPath, string $newPath): bool;

    /**
     * Copy a file
     * @param string $sourcePath Source path
     * @param string $destinationPath Destination path
     * @return bool Success
     * @throws StorageException On failure
     */
    public function copy(string $sourcePath, string $destinationPath): bool;

    /**
     * Check if file exists
     * @param string $path Path to check
     * @return bool Exists
     */
    public function exists(string $path): bool;

    /**
     * Get public URL for a file
     * @param string $path File path
     * @param bool $cacheBust Append random query param to bust cache
     * @return string Full URL
     */
    public function getUrl(string $path, bool $cacheBust = false): string;

    /**
     * Get the base URL for constructing URLs client-side
     * @return string Base URL (e.g., 'dynamic/' or 'https://vowstorage.blob.core.windows.net/vow-dynamic/')
     */
    public function getBaseUrl(): string;
}
